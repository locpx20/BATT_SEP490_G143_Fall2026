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
            public int ProductionTarget { get; set; }
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

            if (!string.IsNullOrEmpty(StatusFilter) && StatusFilter != "ALL")
                query = query.Where(a => a.Status == StatusFilter);

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
                        PlanId = first.AssignmentId,
                        AssignmentDate = g.Key.AssignmentDate,
                        Shift = g.Key.Shift,
                        Assigner = first.AssignedByNavigation,
                        Status = g.Any(x => x.Status == "Completed")
                                    ? "Completed"
                                  : g.Any(x => x.Status == "InProgress")
                                    ? "InProgress"
                                    : first.Status,
                        ProductionTarget = 25000,
                        TaskDescription = first.TaskDescription,
                        Stages = g.OrderBy(x => x.AssignmentId).ToList()
                    };
                })
                .ToList();

            if (!string.IsNullOrEmpty(SearchTerm))
            {
                var kw = SearchTerm.Trim().ToLower();
                grouped = grouped.Where(p =>
                    p.Code.ToLower().Contains(kw) ||
                    p.AssignmentDate.ToString("yyyy-MM-dd").Contains(kw) ||
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
        [BindProperty] public int NewPlanTargetKg { get; set; } = 25000;
        [BindProperty] public string? NewPlanNotes { get; set; }

        [BindProperty] public int Stage1StaffId { get; set; }
        [BindProperty] public int Stage1TargetKg { get; set; } = 28000;

        [BindProperty] public int Stage2StaffId { get; set; }
        [BindProperty] public int Stage2TargetKg { get; set; } = 26000;

        [BindProperty] public int Stage3StaffId { get; set; }
        [BindProperty] public int Stage3TargetKg { get; set; } = 25000;

        [BindProperty] public int Stage4StaffId { get; set; }
        [BindProperty] public int Stage4TargetKg { get; set; } = 25000;

        public async Task<IActionResult> OnPostCreatePlanAsync()
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            if (!ModelState.IsValid || NewPlanTargetKg <= 0)
            {
                ErrorMessage = "Sản lượng mục tiêu phải lớn hơn 0 kg";
                return RedirectToPage();
            }

            var currentUserId = CurrentUserId;

            var stages = new List<(int staffId, string desc, int target)>
            {
                (Stage1StaffId, "Sàng lọc tạp chất & băm dăm bào (Máy băm nghiền M-01)", Stage1TargetKg),
                (Stage2StaffId, "Sấy mùn cưa hạ ẩm (Máy sấy thùng quay M-02)", Stage2TargetKg),
                (Stage3StaffId, "Ép viên nén sinh khối (Cụm máy ép viên M-03A)", Stage3TargetKg),
                (Stage4StaffId, "Đóng bao Jumbo 650kg & lưu kho (Cân đóng bao M-04)", Stage4TargetKg)
            };

            if (stages.Any(s => s.staffId <= 0 || s.target <= 0))
            {
                ErrorMessage = "Vui lòng chọn nhân viên và nhập chỉ tiêu lớn hơn 0 cho từng công đoạn.";
                return RedirectToPage();
            }

            var staffIds = stages.Select(s => s.staffId).Distinct().ToList();
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

            foreach (var (staffId, desc, target) in stages)
            {
                var assignment = new PersonnelAssignment
                {
                    StaffId = staffId,
                    AssignedBy = currentUserId,
                    AssignmentDate = NewPlanDate,
                    Shift = NewPlanShift,
                    TaskDescription = desc,
                    Status = "Assigned"
                };
                _context.PersonnelAssignments.Add(assignment);
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
    }
}
