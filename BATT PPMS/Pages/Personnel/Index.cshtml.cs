using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public bool CanCreatePlan { get; set; }
        public bool IsStaffView => IsStaff;
        public bool IsCurrentWorker(int? staffId) => staffId == CurrentUserId && IsStaff;
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

        // ── Inner DTO ────────────────────────────────────────────────────────
        public class PlanGroup
        {
            public int PlanId { get; set; }
            public string Code => $"PLAN-{PlanId:D4}";
            public DateOnly AssignmentDate { get; set; }
            public string? Shift { get; set; }
            public User? Assigner { get; set; }
            public string Status { get; set; } = "Assigned";
            public string? TaskDescription { get; set; }
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

            CanCreatePlan = IsManager;

            // Load staff list for modal
            AvailableStaff = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName == RoleConstants.Staff && u.Status == StatusConstants.Active)
                .OrderBy(u => u.FullName)
                .ToListAsync();

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

            if (!string.IsNullOrEmpty(ShiftFilter) && ShiftFilter != "ALL")
                query = query.Where(a => a.Shift == ShiftFilter);

            var assignments = await query
                .OrderByDescending(a => a.AssignmentDate)
                .ThenBy(a => a.Shift)
                .ToListAsync();

            // Group by (AssignmentDate + Shift + AssignedBy)
            var grouped = assignments
                .GroupBy(a => new { a.AssignmentDate, a.Shift, a.AssignedBy })
                .Select(g =>
                {
                    var first = g.First();
                    return new PlanGroup
                    {
                        PlanId = g.Min(a => a.AssignmentId),
                        AssignmentDate = g.Key.AssignmentDate,
                        Shift = g.Key.Shift,
                        Assigner = first.AssignedByNavigation,
                        Status = AggregateStatus(g),
                        TaskDescription = first.TaskDescription,
                        Stages = g.OrderBy(x => x.AssignmentId).ToList()
                    };
                })
                .ToList();

            // Filter whole plans so a status filter never removes their coworkers/stages.
            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "ALL")
                grouped = grouped.Where(p => p.Status == NormalizeAssignmentStatus(StatusFilter)).ToList();

            if (!string.IsNullOrEmpty(SearchTerm))
            {
                var kw = SearchTerm.Trim().ToLower();
                grouped = grouped.Where(p =>
                    p.Code.ToLower().Contains(kw) ||
                    p.AssignmentDate.ToString("yyyy-MM-dd").Contains(kw) ||
                    p.AssignmentDate.ToString("dd/MM/yyyy").Contains(kw) ||
                    (p.Assigner?.FullName ?? "").ToLower().Contains(kw) ||
                    (p.Shift ?? "").ToLower().Contains(kw) ||
                    p.Stages.Any(s => (s.Staff?.FullName ?? "").ToLower().Contains(kw) ||
                                      (s.TaskDescription ?? "").ToLower().Contains(kw))
                ).ToList();
            }

            Plans = grouped;
            return Page();
        }

        // ── OnPostCreatePlan ─────────────────────────────────────────────────
        [BindProperty] public DateOnly NewPlanDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        [BindProperty] public string NewPlanShift { get; set; } = "Ca 1";
        [BindProperty] public string? NewPlanNotes { get; set; }

        [BindProperty] public List<int> Stage1StaffIds { get; set; } = new();

        [BindProperty] public List<int> Stage2StaffIds { get; set; } = new();

        [BindProperty] public List<int> Stage3StaffIds { get; set; } = new();

        [BindProperty] public List<int> Stage4StaffIds { get; set; } = new();

        [BindProperty] public List<int> Stage5StaffIds { get; set; } = new();

        public record ProductionStage(int Number, string Name, string Machine, List<int> StaffIds)
        {
            public string FieldName => $"Stage{Number}StaffIds";
            public string TaskDescription => $"{Name} ({Machine})";
        }

        public List<ProductionStage> ProductionStages => new()
        {
            new(1, "Băm nguyên liệu", "Máy băm", Stage1StaffIds),
            new(2, "Nghiền nguyên liệu", "Máy nghiền", Stage2StaffIds),
            new(3, "Sấy nguyên liệu", "Máy sấy", Stage3StaffIds),
            new(4, "Nén viên", "Máy nén", Stage4StaffIds),
            new(5, "Đóng gói thành phẩm", "Khâu đóng gói", Stage5StaffIds)
        };

        public async Task<IActionResult> OnPostCreatePlanAsync()
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            if (!ModelState.IsValid || NewPlanDate.Year < 2000 || NewPlanDate.Year > 2100)
            {
                ErrorMessage = "Vui lòng kiểm tra ngày, ca sản xuất và danh sách công nhân.";
                return RedirectToPage();
            }

            var currentUserId = CurrentUserId;

            var stages = ProductionStages;

            if (stages.Any(s => s.StaffIds == null || s.StaffIds.Count == 0 || s.StaffIds.Any(id => id <= 0)))
            {
                ErrorMessage = "Vui lòng chọn ít nhất một công nhân cho mỗi máy/công đoạn.";
                return RedirectToPage();
            }

            var staffIds = stages.SelectMany(s => s.StaffIds).Distinct().ToList();
            var validStaffCount = await _context.Users.CountAsync(u => staffIds.Contains(u.UserId)
                && u.Role.RoleName == RoleConstants.Staff && u.Status == StatusConstants.Active);
            if (validStaffCount != staffIds.Count)
            {
                ErrorMessage = "Chỉ được phân công cho nhân viên sản xuất (Staff) có tài khoản đang hoạt động.";
                return RedirectToPage();
            }

            if (NewPlanShift is not ("Ca 1" or "Ca 2" or "Ca 3"))
            {
                ErrorMessage = "Vui lòng chọn ca sản xuất hợp lệ.";
                return RedirectToPage();
            }

            foreach (var stage in stages)
            {
                foreach (var staffId in stage.StaffIds.Distinct())
                {
                    var assignment = new PersonnelAssignment
                    {
                        StaffId = staffId,
                        AssignedBy = currentUserId,
                        AssignmentDate = NewPlanDate,
                        Shift = NewPlanShift,
                        TaskDescription = stage.TaskDescription,
                        Status = "Assigned"
                    };
                    _context.PersonnelAssignments.Add(assignment);
                }
            }

            await _context.SaveChangesAsync();
            SuccessMessage = $"Đã tạo kế hoạch {NewPlanShift} ngày {NewPlanDate:dd/MM/yyyy} và gửi phân công cho {staffIds.Count} nhân viên sản xuất.";
            return RedirectToPage();
        }

        public List<PersonnelAssignment> GetStagesForPlan(int planId)
        {
            var plan = Plans.FirstOrDefault(p => p.PlanId == planId);
            return plan?.Stages ?? new List<PersonnelAssignment>();
        }

        public class StageGroup
        {
            public string TaskDescription { get; set; } = string.Empty;
            public string StaffMachineName
            {
                get
                {
                    var description = TaskDescription.ToLowerInvariant();
                    if (description.Contains("đóng gói") || description.Contains("đóng bao")) return "Đóng gói";
                    if (description.Contains("sấy")) return "Máy sấy";
                    if (description.Contains("nén") || description.Contains("ép viên")) return "Máy nén";
                    if (description.Contains("băm")) return "Máy băm";
                    if (description.Contains("nghiền")) return "Máy nghiền";
                    return TaskDescription;
                }
            }
            public List<PersonnelAssignment> Assignments { get; set; } = new();
        }

        public List<StageGroup> GetStageGroupsForPlan(int planId) => GetStagesForPlan(planId)
            .GroupBy(a => a.TaskDescription ?? "Công đoạn sản xuất")
            .Select(group => new StageGroup
            {
                TaskDescription = group.Key,
                Assignments = group.GroupBy(a => a.StaffId).Select(workers => workers.First()).ToList()
            }).ToList();
    }
}
