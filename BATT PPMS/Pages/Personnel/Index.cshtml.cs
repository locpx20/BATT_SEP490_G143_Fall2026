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
        public DateOnly? PreviousDate { get; private set; }
        public DateOnly? NextDate { get; private set; }
        public IReadOnlyDictionary<string, ShiftProductionPlan> ShiftPlans { get; private set; }
            = new Dictionary<string, ShiftProductionPlan>();
        public bool HasLegacyShifts { get; private set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public bool CanCreatePlan { get; set; }
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
            if (Create)
            {
                if (!CanCreatePlan) return RedirectToPage("/Auth/AccessDenied");
                NewPlanDate = PlanDate ?? Today;
                await LoadAvailableStaffAsync();
                return Page();
            }

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
                if (!PlanDate.HasValue && !await query.AnyAsync(a => a.AssignmentDate == Today))
                {
                    var latestDate = await query.Where(a => a.AssignmentDate < Today)
                        .Select(a => (DateOnly?)a.AssignmentDate).MaxAsync();
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
                var dates = _context.PersonnelAssignments.Select(a => a.AssignmentDate);
                PreviousDate = await dates.Where(date => date < day).Select(date => (DateOnly?)date).MaxAsync();
                NextDate = await dates.Where(date => date > day).Select(date => (DateOnly?)date).MinAsync();
                ShiftPlans = await _context.ShiftProductionPlans.AsNoTracking()
                    .Where(plan => plan.PlanDate == day).ToDictionaryAsync(plan => plan.Shift);
                query = query.Where(a => a.AssignmentDate == day);
                if (CanCreatePlan)
                {
                    var plannedShifts = ShiftPlans.Keys.ToArray();
                    HasLegacyShifts = await query.AnyAsync(a => a.Shift == null || !plannedShifts.Contains(a.Shift));
                    if (HasLegacyShifts)
                    {
                        EstimateDate = day;
                        EstimateKg = await _context.DailyMaterialPlans.Where(plan => plan.PlanDate == day)
                            .Select(plan => (decimal?)plan.ExpectedKg).SingleOrDefaultAsync() ?? 0;
                    }
                }
            }

            if (!string.IsNullOrEmpty(ShiftFilter) && ShiftFilter != "ALL")
                query = query.Where(a => a.Shift == ShiftFilter);

            var assignments = await query.OrderByDescending(a => a.AssignmentDate).ThenBy(a => a.Shift).ToListAsync();
            var shiftStatuses = IsStaff && assignments.Count > 0 ? await LoadShiftStatusesAsync(query) : null;

            var grouped = assignments
                .GroupBy(a => new { a.AssignmentDate, a.Shift })
                .Select(g => new PlanGroup
                {
                    PlanId = g.Min(a => a.AssignmentId),
                    AssignmentDate = g.Key.AssignmentDate,
                    Shift = g.Key.Shift,
                    Status = shiftStatuses?.GetValueOrDefault((g.Key.AssignmentDate, g.Key.Shift)) ?? AggregateStatus(g),
                    Stages = g.OrderBy(x => x.AssignmentId).ToList()
                })
                .ToList();

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
                StaffSchedule = BuildStaffSchedule(Plans.SelectMany(plan => plan.Stages), shiftStatuses: shiftStatuses);
            return Page();
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
                HasActiveAssignment = query.Any(assignment => assignment.AssignmentDate == shift.AssignmentDate
                        && assignment.Shift == shift.Shift && assignment.Status != "Cancelled")
            }).ToListAsync(HttpContext.RequestAborted);

            return rows.ToDictionary(row => (row.AssignmentDate, row.Shift), row =>
                !row.HasActiveAssignment ? "Cancelled" :
                row.HasDeduction && row.HasReceipt ? "Completed" :
                row.HasDeduction || row.HasReceipt ? "InProgress" : "Assigned");
        }

        private ProductionScheduleViewModel BuildStaffSchedule(IEnumerable<PersonnelAssignment> assignments,
            bool invalidDate = false, IReadOnlyDictionary<(DateOnly Date, string? Shift), string>? shiftStatuses = null)
        {
            var rows = assignments.OrderByDescending(a => a.AssignmentDate)
                .ThenBy(a => a.Shift).ThenBy(a => a.AssignmentId).ToList();
            var shifts = rows.Select(a => a.Shift).Distinct().OrderBy(shift => shift).ToList();
            var page = new PageWindow(shifts.Count, PageWindow.ReadRequestedPage(Request.Query, PageNumber), pageSize: 1);
            if (!AllPlanShifts && shifts.Count > 0)
                rows = rows.Where(a => a.Shift == shifts[page.Offset]).ToList();

            var filters = new Dictionary<string, string>();
            if (AllDates) filters[nameof(AllDates)] = "true";
            if (!string.IsNullOrWhiteSpace(SearchTerm)) filters[nameof(SearchTerm)] = SearchTerm;
            if (!string.IsNullOrWhiteSpace(StatusFilter)) filters[nameof(StatusFilter)] = StatusFilter;
            var dateInput = !AllDates && Request.Query.ContainsKey(nameof(PlanDate))
                ? PlanDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null;
            return new ProductionScheduleViewModel("/Personnel/Index", filters, DisplayDate, dateInput,
                AllPlanShifts, shifts, rows, page, invalidDate, CurrentUserId, shiftStatuses);
        }

        [BindProperty] public DateOnly NewPlanDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        [BindProperty] public List<ShiftInput> ShiftInputs { get; set; } = [new() { Included = true }, new(), new()];
        [BindProperty] public DateOnly EstimateDate { get; set; } = DateOnly.FromDateTime(ProductionFlow.Now);
        [BindProperty] public decimal EstimateKg { get; set; }

        public sealed class ShiftInput
        {
            public bool Included { get; set; }
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
            new("Băm nguyên liệu", "Máy băm"),
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

        private async Task LoadAvailableStaffAsync()
        {
            AvailableStaff = await _context.Users.AsNoTracking()
                .Where(u => u.Role.RoleName == RoleConstants.Staff && u.Status == StatusConstants.Active)
                .OrderBy(u => u.FullName).ThenBy(u => u.UserId).ToListAsync();
        }

        private async Task<IActionResult> ShowPlanErrorAsync(string message)
        {
            Create = true;
            CanCreatePlan = true;
            ErrorMessage = message;
            ShiftInputs = Enumerable.Range(0, ProductionShifts.Count)
                .Select(i => ShiftInputs.ElementAtOrDefault(i) ?? new ShiftInput()).ToList();
            await LoadAvailableStaffAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostCreatePlanAsync()
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            if (!ModelState.IsValid || NewPlanDate.Year is < 2000 or > 2100 || ShiftInputs.Count != ProductionShifts.Count)
                return await ShowPlanErrorAsync("Vui lòng kiểm tra ngày sản xuất và các trường số lượng.");

            var shifts = ShiftInputs.Select((input, index) => (Name: ProductionShifts[index], Input: input))
                .Where(shift => shift.Input.Included).ToList();
            if (shifts.Count == 0)
                return await ShowPlanErrorAsync("Vui lòng chọn ít nhất một ca để phân công.");

            foreach (var (name, input) in shifts)
            {
                if (!DailyMaterialPlanning.ValidQuantity(input.ExpectedMaterialKg ?? 0)
                    || !DailyMaterialPlanning.ValidQuantity(input.ExpectedFinishedKg ?? 0))
                    return await ShowPlanErrorAsync($"{name} cần khối lượng nguyên liệu và thành phẩm lớn hơn 0 kg, tối đa 2 chữ số thập phân.");
                if (input.StaffByStage.Any(staff => staff.Count == 0 || staff.Any(id => id <= 0)))
                    return await ShowPlanErrorAsync($"Chọn ít nhất một công nhân cho mỗi công đoạn của {name}.");
                if (input.Note?.Length > 1000)
                    return await ShowPlanErrorAsync($"Ghi chú của {name} tối đa 1.000 ký tự.");
            }

            var shiftNames = shifts.Select(shift => shift.Name).ToArray();
            var staffIds = shifts.SelectMany(shift => shift.Input.StaffByStage.SelectMany(ids => ids)).Distinct().ToArray();
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
                    throw new ProductionFlowException($"{string.Join(", ", existing.Order())} đã có phân công ngày {NewPlanDate:dd/MM/yyyy}. Bỏ chọn các ca này rồi lưu lại.");

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
                        ExpectedMaterialKg = input.ExpectedMaterialKg!.Value,
                        ExpectedFinishedKg = input.ExpectedFinishedKg!.Value,
                        Note = InputValidator.Clean(input.Note)
                    });
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

                var keepers = await _context.Users.Where(u => u.Role.RoleName == RoleConstants.WarehouseStaff
                    && u.Status == StatusConstants.Active).Select(u => u.UserId).ToListAsync();
                var projected = shifts.Sum(shift => shift.Input.ExpectedMaterialKg!.Value);
                foreach (var keeper in keepers)
                    Notifier.Add(_context, keeper, "Phân công lao động đã cập nhật",
                        $"Ngày {NewPlanDate:dd/MM/yyyy}, {string.Join(", ", shiftNames)} dự kiến tiêu thụ {projected:N2} kg.",
                        $"/Warehouse/Index?tab=plans&PlanDate={NewPlanDate:yyyy-MM-dd}&AllPlanShifts=true");

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

            SuccessMessage = $"Đã lưu phân công ngày {NewPlanDate:dd/MM/yyyy} cho {shifts.Count} ca.";
            return RedirectToPage(new { PlanDate = NewPlanDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        }

        public async Task<IActionResult> OnPostSaveDailyMaterialAsync() {
            var check=RequireRole(RoleConstants.Manager);if(check!=null)return check;
            if(!ModelState.IsValid){ErrorMessage="Ngày và khối lượng dự kiến không hợp lệ.";return RedirectToPage();}
            await using var tx=await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try{await new DailyMaterialPlanning(_context).Set(CurrentUserId,EstimateDate,EstimateKg);await _context.SaveChangesAsync();await tx.CommitAsync();SuccessMessage=$"Đã cập nhật {EstimateKg:N2} kg dự kiến ngày {EstimateDate:dd/MM/yyyy} cho Thủ kho.";}
            catch(ProductionFlowException ex){ErrorMessage=ex.Message;}
            return RedirectToPage(new { PlanDate = EstimateDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        }

        public static string StaffMachineName(string task)
        {
            var description = task.ToLowerInvariant();
            if (description.Contains("đóng gói") || description.Contains("đóng bao")) return "Đóng gói";
            if (description.Contains("sấy")) return "Máy sấy";
            if (description.Contains("nén") || description.Contains("ép viên")) return "Máy nén";
            if (description.Contains("băm")) return "Máy băm";
            if (description.Contains("nghiền")) return "Máy nghiền";
            return task;
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
