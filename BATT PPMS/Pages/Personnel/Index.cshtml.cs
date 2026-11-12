using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using BATT_PPMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BATT_PPMS.Pages.Personnel
{
    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        // ── Output ──────────────────────────────────────────────────────────
        public List<PlanGroup> Plans { get; set; } = new();
        public List<User> AvailableStaff { get; set; } = new();
        public IReadOnlyDictionary<string, ShiftProductionPlan> ShiftPlans { get; private set; }
            = new Dictionary<string, ShiftProductionPlan>();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public bool CanCreatePlan { get; set; }
        public bool IsEditingPlan => !string.IsNullOrEmpty(EditShift);
        public bool IsStaffView => IsStaff;
        public ProductionScheduleViewModel? StaffSchedule { get; private set; }
        public static string NormalizeAssignmentStatus(string? status) => status switch
        {
            "Done" or "Completed" => "Completed",
            "In Progress" or "InProgress" => "InProgress",
            "Cancelled" => "Cancelled",
            "Assigned" or null or "" => "Assigned",
            _ => status
        };
        public static string AssignmentStatusLabel(string? status) => NormalizeAssignmentStatus(status) switch
        {
            "Completed" => "Hoàn thành",
            "InProgress" => "Đang thực hiện",
            "Cancelled" => "Đã hủy",
            "Off" => "Nghỉ",
            "Assigned" => "Chờ thực hiện",
            _ => "Chưa xác định"
        };
        public static string AggregateStatus(IEnumerable<PersonnelAssignment> assignments)
        {
            var statuses = assignments.Select(a => NormalizeAssignmentStatus(a.Status)).ToList();
            if (statuses.Count == 0) return "Assigned";
            if (statuses.All(s => s == "Cancelled")) return "Cancelled";
            var active = statuses.Where(s => s != "Cancelled").ToList();
            if (active.All(s => s == "Completed")) return "Completed";
            if (active.Any(s => s is "InProgress" or "Completed")) return "InProgress";
            return "Assigned";
        }
        public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        public DateOnly EarliestNewPlanDate => Today.AddDays(1);
        public DateOnly? DisplayDate { get; private set; }
        public bool ShowingLatestPlanDate { get; private set; }

        // ── Filters ──────────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public string? SearchTerm { get; set; }
        [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? ShiftFilter { get; set; }
        [BindProperty(SupportsGet = true)] public DateOnly? PlanDate { get; set; }
        [BindProperty(SupportsGet = true)] public bool AllDates { get; set; }
        [BindProperty(SupportsGet = true)] public bool AllPlanShifts { get; set; } = true;
        [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
        [BindProperty(SupportsGet = true)] public bool Create { get; set; }
        [BindProperty(SupportsGet = true)] public bool MarkDayOff { get; set; }
        [BindProperty(SupportsGet = true)] public string? EditShift { get; set; }

        // ── Inner DTO ────────────────────────────────────────────────────────
        public class PlanGroup
        {
            public int PlanId { get; set; }
            public IEnumerable<string> Codes => Stages.GroupBy(a => a.AssignedBy)
                .Select(group => $"PLAN-{group.Min(a => a.AssignmentId):D4}");
            public DateOnly AssignmentDate { get; set; }
            public string? Shift { get; set; }
            public string Status { get; set; } = "Assigned";
            public List<PersonnelAssignment> Stages { get; set; } = new();
        }

        // ── OnGet ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(
                RoleConstants.Owner,
                RoleConstants.Manager,
                RoleConstants.Staff);
            if (check != null) return check;

            if (IsStaff && !AllDates && Request.Query.ContainsKey(nameof(PlanDate)) && !PlanDate.HasValue)
            {
                StaffSchedule = BuildStaffSchedule([], invalidDate: true);
                return Page();
            }

            CanCreatePlan = IsManager;
            if (IsEditingPlan)
                return await OpenPlanEditorAsync();
            if ((Create || MarkDayOff) && !CanCreatePlan)
                return RedirectToPage("/Auth/AccessDenied");
            if (CanCreatePlan)
            {
                Create |= MarkDayOff;
                NewPlanDate = MarkDayOff ? Today : PlanDate is { } day && day >= EarliestNewPlanDate
                    ? day : EarliestNewPlanDate;
                if (MarkDayOff)
                    ShiftInputs = [new(), new(), new()];
                else
                    await LoadAvailableStaffAsync();
            }
            await LoadScheduleAsync();
            return Page();
        }

        private async Task LoadScheduleAsync()
        {
            // Load all assignments with navigation
            var query = _context.PersonnelAssignments
                .Include(a => a.Staff)
                .Include(a => a.AssignedByNavigation)
                .AsNoTracking()
                .AsQueryable();

            if (IsStaff && !AllDates)
            {
                // Prefer today; after midnight keep the most recent plan accessible.
                // Never override an explicitly selected date or relabel an old plan as today's.
                DisplayDate = PlanDate ?? Today;
                if (!PlanDate.HasValue && !await query.AnyAsync(a => a.AssignmentDate == Today)
                    && !await _context.ShiftProductionPlans.AnyAsync(p => p.PlanDate == Today && p.IsDayOff))
                {
                    var latestDate = await query.Select(a => (DateOnly?)a.AssignmentDate)
                        .Union(_context.ShiftProductionPlans.Where(p => p.IsDayOff).Select(p => (DateOnly?)p.PlanDate))
                        .Where(date => date < Today).MaxAsync();
                    if (latestDate.HasValue)
                    {
                        DisplayDate = latestDate;
                        ShowingLatestPlanDate = true;
                    }
                }
                PlanDate = DisplayDate;
                query = query.Where(a => a.AssignmentDate == DisplayDate.Value);
            }
            else if (!IsStaff)
            {
                var day = PlanDate ?? Today;
                DisplayDate = day;
                ShiftPlans = await _context.ShiftProductionPlans.AsNoTracking()
                    .Where(plan => plan.PlanDate == day).ToDictionaryAsync(plan => plan.Shift);
                query = query.Where(a => a.AssignmentDate == day);
            }

            if (!string.IsNullOrEmpty(ShiftFilter) && ShiftFilter != "ALL")
                query = query.Where(a => a.Shift == ShiftFilter);

            var daysOff = IsStaff
                ? await _context.ShiftProductionPlans.AsNoTracking()
                    .Where(p => p.IsDayOff && (AllDates || p.PlanDate == DisplayDate)).ToListAsync()
                : ShiftPlans.Values.Where(p => p.IsDayOff).ToList();
            if (!string.IsNullOrEmpty(ShiftFilter) && ShiftFilter != "ALL")
                daysOff = daysOff.Where(p => p.Shift == ShiftFilter).ToList();
            var offShifts = daysOff.Select(p => (p.PlanDate, p.Shift)).ToHashSet();

            var assignments = await query.OrderByDescending(a => a.AssignmentDate).ThenBy(a => a.Shift).ToListAsync();
            var shiftStatuses = (IsStaff || IsManager) && assignments.Count > 0
                ? await LoadShiftStatusesAsync(query)
                : null;

            var grouped = assignments
                .Where(a => a.Shift == null || !offShifts.Contains((a.AssignmentDate, a.Shift)))
                .GroupBy(a => new { a.AssignmentDate, a.Shift })
                .Select(g => new PlanGroup
                {
                    PlanId = g.Min(a => a.AssignmentId),
                    AssignmentDate = g.Key.AssignmentDate,
                    Shift = g.Key.Shift,
                    Status = shiftStatuses?.GetValueOrDefault((g.Key.AssignmentDate, g.Key.Shift)) ?? AggregateStatus(g),
                    Stages = g.OrderBy(x => x.AssignmentId).ToList()
                })
                .Concat(daysOff.Select((p, index) => new PlanGroup
                {
                    PlanId = -1 - index,
                    AssignmentDate = p.PlanDate,
                    Shift = p.Shift,
                    Status = "Off"
                }))
                .OrderByDescending(p => p.AssignmentDate).ThenBy(p => p.Shift).ToList();

            // Filter whole plans so a status filter never removes their coworkers/stages.
            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "ALL")
                grouped = grouped.Where(p => p.Status == NormalizeAssignmentStatus(StatusFilter)).ToList();

            if (!string.IsNullOrEmpty(SearchTerm))
            {
                var kw = SearchTerm.Trim().ToLower();
                grouped = grouped.Where(p =>
                    p.Codes.Any(code => code.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                    p.AssignmentDate.ToString("yyyy-MM-dd").Contains(kw) ||
                    p.AssignmentDate.ToString("dd/MM/yyyy").Contains(kw) ||
                    (p.Shift ?? "").ToLower().Contains(kw) ||
                    p.Stages.Any(s => (s.Staff?.FullName ?? "").ToLower().Contains(kw) ||
                                      (s.AssignedByNavigation?.FullName ?? "").ToLower().Contains(kw) ||
                                      (s.TaskDescription ?? "").ToLower().Contains(kw))
                ).ToList();
            }

            Plans = grouped;
            if (IsStaff)
            {
                var visibleOffShifts = Plans.Where(p => p.Status == "Off")
                    .Select(p => (p.AssignmentDate, p.Shift)).ToHashSet();
                StaffSchedule = BuildStaffSchedule(Plans.SelectMany(plan => plan.Stages), shiftStatuses: shiftStatuses,
                    daysOff: daysOff.Where(p => visibleOffShifts.Contains((p.PlanDate, p.Shift))).ToList());
            }
        }

        private async Task<Dictionary<(DateOnly Date, string? Shift), string>> LoadShiftStatusesAsync(IQueryable<PersonnelAssignment> query)
        {
            var shifts = query.Select(assignment => new { assignment.AssignmentDate, assignment.Shift }).Distinct();
            var rows = await shifts.Select(shift => new
            {
                shift.AssignmentDate,
                shift.Shift,
                HasDeduction = _context.MaterialUsageReviews.Any(review => review.Status == "Approved"
                        && review.Report.ShiftDate == shift.AssignmentDate
                        && review.Report.Assignment.Shift == shift.Shift
                        && _context.ShiftMaterialUsages.Any(usage => usage.ReportId == review.ReportId && usage.ExportId != null)
                        && !_context.ShiftMaterialUsages.Any(usage => usage.ReportId == review.ReportId && usage.ExportId == null)),
                HasReceipt = _context.FinishedGoodsReceipts.Any(receipt =>
                        receipt.QualityRequest.Assignment.AssignmentDate == shift.AssignmentDate
                        && receipt.QualityRequest.Assignment.Shift == shift.Shift
                        && receipt.ReceivedAt != null && receipt.ActualQuantity > 0
                        && (receipt.Status == "Received" || receipt.Status == "AwaitingStaffConfirmation")),
                HasOutstandingLots = _context.ProductionQualityRequests.Any(request =>
                        request.Assignment.AssignmentDate == shift.AssignmentDate
                        && request.Assignment.Shift == shift.Shift
                        && (request.Inspection.Status != "Completed" || request.Inspection.Result != "Fail")
                        && (request.Receipt == null || request.Receipt.ReceivedAt == null
                            || request.Receipt.ActualQuantity == null || request.Receipt.ActualQuantity <= 0
                            || (request.Receipt.Status != "Received" && request.Receipt.Status != "AwaitingStaffConfirmation"))),
                HasActiveAssignment = query.Any(assignment => assignment.AssignmentDate == shift.AssignmentDate
                        && assignment.Shift == shift.Shift && assignment.Status != "Cancelled")
            }).ToListAsync(HttpContext.RequestAborted);

            return rows.ToDictionary(row => (row.AssignmentDate, row.Shift), row =>
                !row.HasActiveAssignment ? "Cancelled" :
                row.HasDeduction && row.HasReceipt && !row.HasOutstandingLots ? "Completed" :
                row.HasDeduction || row.HasReceipt ? "InProgress" : "Assigned");
        }

        private ProductionScheduleViewModel BuildStaffSchedule(IEnumerable<PersonnelAssignment> assignments,
            bool invalidDate = false, IReadOnlyDictionary<(DateOnly Date, string? Shift), string>? shiftStatuses = null,
            IReadOnlyList<ShiftProductionPlan>? daysOff = null)
        {
            daysOff ??= [];
            var rows = assignments.OrderByDescending(a => a.AssignmentDate)
                .ThenBy(a => a.Shift).ThenBy(a => a.AssignmentId).ToList();
            var shifts = rows.Select(a => a.Shift).Union(daysOff.Select(p => (string?)p.Shift)).OrderBy(shift => shift).ToList();
            var page = new PageWindow(shifts.Count, PageWindow.ReadRequestedPage(Request.Query, PageNumber), pageSize: 1);
            if (!AllPlanShifts && shifts.Count > 0)
            {
                var selected = shifts[page.Offset];
                rows = rows.Where(a => a.Shift == selected).ToList();
                daysOff = daysOff.Where(p => p.Shift == selected).ToList();
            }

            var filters = new Dictionary<string, string>();
            if (AllDates) filters[nameof(AllDates)] = "true";
            if (!string.IsNullOrWhiteSpace(SearchTerm)) filters[nameof(SearchTerm)] = SearchTerm;
            if (!string.IsNullOrWhiteSpace(StatusFilter)) filters[nameof(StatusFilter)] = StatusFilter;
            var dateInput = !AllDates && Request.Query.ContainsKey(nameof(PlanDate))
                ? PlanDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null;
            return new ProductionScheduleViewModel("/Personnel/Index", filters, DisplayDate, dateInput,
                AllPlanShifts, shifts, rows, page, invalidDate, CurrentUserId, shiftStatuses, DaysOff: daysOff);
        }

        [BindProperty] public DateOnly NewPlanDate { get; set; }
        [BindProperty] public List<ShiftInput> ShiftInputs { get; set; } = [new() { Included = true }, new(), new()];
        [BindProperty] public DateOnly EstimateDate { get; set; } = DateOnly.FromDateTime(ProductionFlow.Now);
        [BindProperty] public decimal EstimateKg { get; set; }

        public enum ShiftMode { Skip, Work, Off }

        public sealed class ShiftInput
        {
            public bool Included { get; set; }
            public ShiftMode? Mode { get; set; }
            public ShiftMode SelectedMode => Mode ?? (Included ? ShiftMode.Work : ShiftMode.Skip);
            public bool IsDayOff => Mode == ShiftMode.Off;
            public decimal? ExpectedMaterialKg { get; set; }
            public decimal? ExpectedFinishedKg { get; set; }
            public string? Note { get; set; }
            public List<int> Stage1StaffIds { get; set; } = [];
            public List<int> Stage2StaffIds { get; set; } = [];
            public List<int> Stage3StaffIds { get; set; } = [];
            public List<int> Stage4StaffIds { get; set; } = [];
            public List<int> Stage5StaffIds { get; set; } = [];

            public List<int>[] StaffByStage => [Stage1StaffIds, Stage2StaffIds, Stage3StaffIds, Stage4StaffIds, Stage5StaffIds];
        }

        public sealed record ProductionStage(string Name, string Machine)
        {
            public string TaskDescription => $"{Name} ({Machine})";
        }

        public static IReadOnlyList<ProductionStage> ProductionStages { get; } =
        [
            new("Băm nguyên liệu", ProductionAssignmentTasks.Chipper),
            new("Nghiền nguyên liệu", "Máy nghiền"),
            new("Sấy nguyên liệu", "Máy sấy"),
            new("Nén viên", "Máy nén"),
            new("Đóng gói thành phẩm", "Khâu đóng gói")
        ];

        public static IReadOnlyList<string> ProductionShifts { get; } = ["Ca 1", "Ca 2", "Ca 3"];
        public static string ShiftHours(string? shift) => shift switch
        {
            "Ca 1" => "06:00 – 14:00",
            "Ca 2" => "14:00 – 22:00",
            "Ca 3" => "22:00 – 06:00",
            _ => ""
        };

        private IQueryable<PersonnelAssignment> EditableAssignments(DateOnly day, string shift) =>
            _context.PersonnelAssignments.Where(a => a.AssignmentDate == day && a.Shift == shift && a.Status != "Cancelled");

        private async Task<IActionResult> OpenPlanEditorAsync()
        {
            if (!CanCreatePlan) return RedirectToPage("/Auth/AccessDenied");
            var day = PlanDate ?? Today;
            var shift = EditShift;
            if (day != Today || shift == null || !ProductionShifts.Contains(shift))
                return PlanEditUnavailable(day);

            var assignments = await EditableAssignments(day, shift).AsNoTracking().ToListAsync();
            var projection = await _context.ShiftProductionPlans.AsNoTracking()
                .SingleOrDefaultAsync(p => p.PlanDate == day && p.Shift == shift);
            if (assignments.Count == 0 && projection?.IsDayOff != true)
                return PlanEditUnavailable(day, "Ca này chưa có lịch để chỉnh sửa.");

            NewPlanDate = day;
            var input = new ShiftInput
            {
                Mode = projection?.IsDayOff == true ? ShiftMode.Off : ShiftMode.Work,
                ExpectedMaterialKg = projection?.IsDayOff == true ? null : projection?.ExpectedMaterialKg,
                ExpectedFinishedKg = projection?.IsDayOff == true ? null : projection?.ExpectedFinishedKg,
                Note = projection?.Note
            };
            var workersByStage = input.StaffByStage;
            for (var i = 0; i < ProductionStages.Count; i++)
            {
                workersByStage[i].AddRange(AssignmentsForStage(assignments, i)
                    .Where(a => a.StaffId.HasValue).Select(a => a.StaffId!.Value).Distinct());
            }
            ShiftInputs = [input];
            await LoadAvailableStaffAsync(assignments.Select(a => a.StaffId).OfType<int>().Distinct().ToArray());
            return Page();
        }

        private static IEnumerable<PersonnelAssignment> AssignmentsForStage(IEnumerable<PersonnelAssignment> rows, int stageIndex)
        {
            var name = ProductionAssignmentTasks.MachineName(ProductionStages[stageIndex].TaskDescription);
            return rows.Where(row => ProductionAssignmentTasks.MachineName(row.TaskDescription) == name);
        }

        private async Task LoadAvailableStaffAsync(int[]? assignedStaff = null)
        {
            assignedStaff ??= [];
            AvailableStaff = await _context.Users.AsNoTracking()
                .Include(u => u.Role)
                .Where(u => (u.Role.RoleName == RoleConstants.Staff && u.Status == StatusConstants.Active) || assignedStaff.Contains(u.UserId))
                .OrderBy(u => u.FullName).ThenBy(u => u.UserId).ToListAsync();
        }

        private async Task<IActionResult> ShowPlanErrorAsync(string message)
        {
            Create = !IsEditingPlan;
            CanCreatePlan = true;
            ErrorMessage = message;
            ShiftInputs = Enumerable.Range(0, IsEditingPlan ? 1 : ProductionShifts.Count)
                .Select(i => ShiftInputs.ElementAtOrDefault(i) ?? new ShiftInput()).ToList();
            var assignedStaff = IsEditingPlan
                ? await EditableAssignments(NewPlanDate, EditShift!).Where(a => a.StaffId.HasValue)
                    .Select(a => a.StaffId!.Value).Distinct().ToArrayAsync()
                : [];
            if (!MarkDayOff || IsEditingPlan)
                await LoadAvailableStaffAsync(assignedStaff);
            if (Create) await LoadScheduleAsync();
            return Page();
        }

        private IActionResult PlanEditUnavailable(DateOnly day, string message = "Chỉ được chỉnh sửa phân công của ngày hôm nay.")
        {
            ErrorMessage = message;
            return RedirectToPage(new { PlanDate = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        }

        private string? ValidateShiftInput(string shift, ShiftInput input)
        {
            if (input.Note?.Length > 1000) return $"Ghi chú của {shift} tối đa 1.000 ký tự.";
            if (input.IsDayOff)
                return NewPlanDate == Today ? null : "Chỉ được đánh dấu nghỉ cho ca của ngày hôm nay.";
            if (!DailyMaterialPlanning.ValidQuantity(input.ExpectedMaterialKg ?? 0)
                || !DailyMaterialPlanning.ValidQuantity(input.ExpectedFinishedKg ?? 0))
                return $"{shift} cần khối lượng nguyên liệu và thành phẩm lớn hơn 0 kg, tối đa 2 chữ số thập phân.";
            if (input.StaffByStage.Any(staff => staff.Count == 0 || staff.Any(id => id <= 0)))
                return $"Chọn ít nhất một công nhân cho mỗi công đoạn của {shift}.";
            return null;
        }

        private async Task<bool> HasShiftRecordsAsync(IQueryable<PersonnelAssignment> assignments)
        {
            var ids = assignments.Select(a => a.AssignmentId);
            return await _context.EndOfShiftReports.AnyAsync(r => ids.Contains(r.AssignmentId))
                || await _context.ProductionQualityRequests.AnyAsync(r => ids.Contains(r.AssignmentId))
                || await _context.ProductionMaterialRequests.AnyAsync(r => ids.Contains(r.AssignmentId));
        }

        private string? ValidateNewPlanDate() => MarkDayOff
            ? (NewPlanDate == Today ? null : "Chỉ được đánh dấu nghỉ cho ca của ngày hôm nay.")
            : (NewPlanDate >= EarliestNewPlanDate ? null : "Chỉ được tạo phân công công việc từ ngày mai.");

        public async Task<IActionResult> OnPostCreatePlanAsync()
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;
            EditShift = null;

            if (!ModelState.IsValid || NewPlanDate.Year is < 2000 or > 2100 || ShiftInputs.Count != ProductionShifts.Count)
                return await ShowPlanErrorAsync("Vui lòng kiểm tra ngày sản xuất và các trường số lượng.");
            if (ValidateNewPlanDate() is { } dateError)
                return await ShowPlanErrorAsync(dateError);

            var shifts = ShiftInputs.Select((input, index) => (Name: ProductionShifts[index], Input: input))
                .Where(shift => shift.Input.SelectedMode != ShiftMode.Skip).ToList();
            if (shifts.Count == 0)
                return await ShowPlanErrorAsync(MarkDayOff ? "Vui lòng chọn ít nhất một ca nghỉ." : "Vui lòng chọn ít nhất một ca để phân công.");
            if (MarkDayOff && shifts.Any(shift => !shift.Input.IsDayOff))
                return await ShowPlanErrorAsync("Chỉ được chọn Nghỉ hoặc Bỏ qua khi đánh dấu ca nghỉ.");

            foreach (var (name, input) in shifts)
            {
                if (ValidateShiftInput(name, input) is { } error)
                    return await ShowPlanErrorAsync(error);
            }

            var shiftNames = shifts.Select(shift => shift.Name).ToArray();
            var offNames = shifts.Where(shift => shift.Input.IsDayOff).Select(shift => shift.Name).ToArray();
            var workingShifts = shifts.Where(shift => !shift.Input.IsDayOff).ToList();
            var staffIds = workingShifts.SelectMany(shift => shift.Input.StaffByStage.SelectMany(ids => ids)).Distinct().ToArray();
            var manager = CurrentUserId;
            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var activeManager = await _context.Users.AnyAsync(u => u.UserId == manager
                    && u.Role.RoleName == RoleConstants.Manager && u.Status == StatusConstants.Active);
                if (!activeManager) return RedirectToPage("/Auth/AccessDenied");

                var existing = await _context.PersonnelAssignments
                    .Where(a => a.AssignmentDate == NewPlanDate && shiftNames.Contains(a.Shift) && a.Status != "Cancelled")
                    .Select(a => a.Shift).Union(_context.ShiftProductionPlans
                        .Where(plan => plan.PlanDate == NewPlanDate && shiftNames.Contains(plan.Shift)).Select(plan => plan.Shift))
                    .ToListAsync();
                if (existing.Count > 0)
                    throw new ProductionFlowException($"{string.Join(", ", existing.Order())} đã có lịch ngày {NewPlanDate:dd/MM/yyyy}. Chọn Bỏ qua cho các ca này rồi lưu lại.");
                if (offNames.Length > 0 && await HasShiftRecordsAsync(_context.PersonnelAssignments
                    .Where(a => a.AssignmentDate == NewPlanDate && offNames.Contains(a.Shift))))
                    throw new ProductionFlowException("Ca đã có báo cáo hoặc yêu cầu nghiệp vụ không thể chuyển sang nghỉ.");

                var activeStaff = await _context.Users.CountAsync(u => staffIds.Contains(u.UserId)
                    && u.Role.RoleName == RoleConstants.Staff && u.Status == StatusConstants.Active);
                if (activeStaff != staffIds.Length)
                    throw new ProductionFlowException("Danh sách có tài khoản không còn hoạt động hoặc không thuộc vai trò nhân viên sản xuất.");

                foreach (var (name, input) in shifts)
                {
                    _context.ShiftProductionPlans.Add(new ShiftProductionPlan
                    {
                        PlanDate = NewPlanDate,
                        Shift = name,
                        IsDayOff = input.IsDayOff,
                        ExpectedMaterialKg = input.IsDayOff ? 0 : input.ExpectedMaterialKg!.Value,
                        ExpectedFinishedKg = input.IsDayOff ? 0 : input.ExpectedFinishedKg!.Value,
                        Note = InputValidator.Clean(input.Note)
                    });
                    if (input.IsDayOff) continue;
                    var assignments = input.StaffByStage.SelectMany((ids, index) => ids.Distinct().Select(id =>
                        new PersonnelAssignment
                        {
                            StaffId = id,
                            AssignedBy = manager,
                            AssignmentDate = NewPlanDate,
                            Shift = name,
                            TaskDescription = ProductionStages[index].TaskDescription,
                            Status = "Assigned"
                        }));
                    _context.PersonnelAssignments.AddRange(assignments);
                }

                var projected = workingShifts.Sum(shift => shift.Input.ExpectedMaterialKg!.Value);
                await NotifyWarehouseOfPlanAsync(workingShifts.Select(shift => shift.Name), projected, offNames);

                if (ValidateNewPlanDate() is { } expiredDateError)
                    throw new ProductionFlowException(expiredDateError);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (ProductionFlowException ex)
            {
                return await ShowPlanErrorAsync(ex.Message);
            }
            catch (Exception ex) when (ex is SqlException { Number: 1205 or 2601 or 2627 }
                or DbUpdateException { InnerException: SqlException { Number: 1205 or 2601 or 2627 } })
            {
                return await ShowPlanErrorAsync("Lịch ngày này vừa được cập nhật. Kiểm tra các ca đã phân công trước khi lưu lại.");
            }

            SuccessMessage = $"Đã lưu lịch ngày {NewPlanDate:dd/MM/yyyy} cho {shifts.Count} ca.";
            return RedirectToPage(new { PlanDate = NewPlanDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        }

        public async Task<IActionResult> OnPostEditPlanAsync()
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;
            var day = NewPlanDate;
            var shift = EditShift;
            if (day != Today || shift == null || !ProductionShifts.Contains(shift))
                return PlanEditUnavailable(day);
            if (!ModelState.IsValid || ShiftInputs.Count != 1)
                return await ShowPlanErrorAsync("Vui lòng kiểm tra các trường số lượng và phân công.");

            var input = ShiftInputs[0];
            if (input.Mode == ShiftMode.Skip)
                return await ShowPlanErrorAsync("Vui lòng chọn Phân công hoặc Nghỉ cho ca này.");
            if (ValidateShiftInput(shift, input) is { } error)
                return await ShowPlanErrorAsync(error);

            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var manager = CurrentUserId;
                if (!await _context.Users.AnyAsync(u => u.UserId == manager
                    && u.Role.RoleName == RoleConstants.Manager && u.Status == StatusConstants.Active))
                    return RedirectToPage("/Auth/AccessDenied");

                var assignments = await EditableAssignments(day, shift).ToListAsync();
                var projection = await _context.ShiftProductionPlans
                    .SingleOrDefaultAsync(p => p.PlanDate == day && p.Shift == shift);
                if (assignments.Count == 0 && projection?.IsDayOff != true)
                    return PlanEditUnavailable(day, "Ca này chưa có lịch để chỉnh sửa.");

                var removed = new List<PersonnelAssignment>();
                var added = new List<PersonnelAssignment>();
                if (input.IsDayOff)
                {
                    if (await HasShiftRecordsAsync(_context.PersonnelAssignments
                        .Where(a => a.AssignmentDate == day && a.Shift == shift)))
                        throw new ProductionFlowException("Ca đã có báo cáo hoặc yêu cầu nghiệp vụ không thể chuyển sang nghỉ.");
                    removed.AddRange(assignments);
                }
                else
                {
                    var workersByStage = input.StaffByStage;
                    for (var i = 0; i < ProductionStages.Count; i++)
                    {
                        var stageRows = AssignmentsForStage(assignments, i).Where(a => a.StaffId.HasValue).ToList();
                        var selected = workersByStage[i].ToHashSet();
                        removed.AddRange(stageRows.Where(a => !selected.Contains(a.StaffId!.Value)));
                        foreach (var staffId in selected.Except(stageRows.Select(a => a.StaffId!.Value)))
                        {
                            added.Add(new PersonnelAssignment
                            {
                                StaffId = staffId,
                                AssignedBy = manager,
                                AssignmentDate = day,
                                Shift = shift,
                                TaskDescription = ProductionStages[i].TaskDescription,
                                Status = "Assigned"
                            });
                        }
                    }
                }

                var addedStaff = added.Select(a => a.StaffId!.Value).Distinct().ToArray();
                if (addedStaff.Length > 0 && await _context.Users.CountAsync(u => addedStaff.Contains(u.UserId)
                    && u.Role.RoleName == RoleConstants.Staff && u.Status == StatusConstants.Active) != addedStaff.Length)
                    throw new ProductionFlowException("Chỉ được thêm công nhân có tài khoản đang hoạt động.");

                var removedIds = removed.Select(a => a.AssignmentId).ToArray();
                if (!input.IsDayOff && removedIds.Length > 0 && await HasShiftRecordsAsync(_context.PersonnelAssignments
                    .Where(a => removedIds.Contains(a.AssignmentId))))
                    throw new ProductionFlowException("Giữ lại công nhân ở công đoạn đã phát sinh báo cáo hoặc yêu cầu.");

                var note = InputValidator.Clean(input.Note);
                var materialKg = input.IsDayOff ? 0 : input.ExpectedMaterialKg!.Value;
                var finishedKg = input.IsDayOff ? 0 : input.ExpectedFinishedKg!.Value;
                var changed = projection == null || removed.Count > 0 || added.Count > 0 || projection.IsDayOff != input.IsDayOff
                    || projection.ExpectedMaterialKg != materialKg || projection.ExpectedFinishedKg != finishedKg || projection.Note != note;
                if (changed)
                {
                    if (projection == null)
                    {
                        projection = new ShiftProductionPlan { PlanDate = day, Shift = shift };
                        _context.ShiftProductionPlans.Add(projection);
                    }
                    projection.IsDayOff = input.IsDayOff;
                    projection.ExpectedMaterialKg = materialKg;
                    projection.ExpectedFinishedKg = finishedKg;
                    projection.Note = note;
                    _context.PersonnelAssignments.RemoveRange(removed);
                    _context.PersonnelAssignments.AddRange(added);
                    await NotifyWarehouseOfPlanAsync(input.IsDayOff ? [] : [shift], materialKg, input.IsDayOff ? [shift] : []);
                    if (day != Today) return PlanEditUnavailable(day);
                    await _context.SaveChangesAsync();
                }
                await transaction.CommitAsync();
            }
            catch (ProductionFlowException ex)
            {
                return await ShowPlanErrorAsync(ex.Message);
            }
            catch (Exception ex) when (ex is SqlException { Number: 1205 or 2601 or 2627 }
                or DbUpdateException { InnerException: SqlException { Number: 1205 or 2601 or 2627 } })
            {
                return await ShowPlanErrorAsync("Lịch ngày này vừa được cập nhật. Kiểm tra phân công trước khi lưu lại.");
            }

            SuccessMessage = input.IsDayOff ? $"Đã đánh dấu {shift} ngày {day:dd/MM/yyyy} là ca nghỉ."
                : $"Đã lưu phân công {shift} ngày {day:dd/MM/yyyy}.";
            return RedirectToPage(new { PlanDate = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ShiftFilter = shift });
        }

        private async Task NotifyWarehouseOfPlanAsync(IEnumerable<string> workingShifts, decimal projected, string[] daysOff)
        {
            var details = new List<string>();
            if (projected > 0) details.Add($"{string.Join(", ", workingShifts)} dự kiến tiêu thụ {projected:N2} kg.");
            if (daysOff.Length > 0) details.Add($"{string.Join(", ", daysOff)} nghỉ.");
            var keepers = await _context.Users.Where(u => u.Role.RoleName == RoleConstants.WarehouseStaff
                && u.Status == StatusConstants.Active).Select(u => u.UserId).ToListAsync();
            foreach (var keeper in keepers)
                Notifier.Add(_context, keeper, "Phân công lao động đã cập nhật",
                    $"Ngày {NewPlanDate:dd/MM/yyyy}, {string.Join(" ", details)}",
                    $"/Warehouse/Index?tab=plans&PlanDate={NewPlanDate:yyyy-MM-dd}&AllPlanShifts=true");
        }

        public async Task<IActionResult> OnPostSaveDailyMaterialAsync() {
            var check=RequireRole(RoleConstants.Manager);if(check!=null)return check;
            if(!ModelState.IsValid){ErrorMessage="Ngày và khối lượng dự kiến không hợp lệ.";return RedirectToPage();}
            if (EstimateDate != Today) return PlanEditUnavailable(EstimateDate);
            await using var tx=await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try{await new DailyMaterialPlanning(_context).Set(CurrentUserId,EstimateDate,EstimateKg);await _context.SaveChangesAsync();await tx.CommitAsync();SuccessMessage=$"Đã cập nhật {EstimateKg:N2} kg dự kiến ngày {EstimateDate:dd/MM/yyyy} cho Thủ kho.";}
            catch(ProductionFlowException ex){ErrorMessage=ex.Message;}
            return RedirectToPage(new { PlanDate = EstimateDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        }

        public class StageGroup
        {
            public string TaskDescription { get; set; } = string.Empty;
            public List<PersonnelAssignment> Assignments { get; set; } = new();
        }

        public static List<StageGroup> GetStageGroupsForPlan(PlanGroup plan) => plan.Stages
            .GroupBy(a => a.TaskDescription ?? "Công đoạn sản xuất", StringComparer.OrdinalIgnoreCase)
            .Select(group => new StageGroup
            {
                TaskDescription = group.Key,
                Assignments = group.GroupBy(a => a.StaffId).Select(workers => workers.First()).ToList()
            }).ToList();
    }
}
