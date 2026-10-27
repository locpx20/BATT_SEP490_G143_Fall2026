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

        // ── Filters ──────────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public string? SearchTerm { get; set; }
        [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? ShiftFilter { get; set; }

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

            CanCreatePlan = IsOwnerOrManager;

            // Load staff list for modal
            AvailableStaff = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role != null && (u.Role.RoleName == "Staff" || u.Role.RoleName == "Manager"))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // Load all assignments with navigation
            var query = _context.PersonnelAssignments
                .Include(a => a.Staff)
                .Include(a => a.AssignedByNavigation)
                .AsNoTracking()
                .AsQueryable();

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
        [BindProperty] public DateOnly NewPlanDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
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
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (NewPlanTargetKg <= 0)
            {
                ErrorMessage = "Sản lượng mục tiêu phải lớn hơn 0 kg";
                return RedirectToPage();
            }

            var currentUserId = CurrentUserId > 0 ? CurrentUserId : 1;

            var stages = new List<(int staffId, string desc, int target)>
            {
                (Stage1StaffId, "Sàng lọc tạp chất & băm dăm bào (Máy băm nghiền M-01)", Stage1TargetKg),
                (Stage2StaffId, "Sấy mùn cưa hạ ẩm (Máy sấy thùng quay M-02)", Stage2TargetKg),
                (Stage3StaffId, "Ép viên nén sinh khối (Cụm máy ép viên M-03A)", Stage3TargetKg),
                (Stage4StaffId, "Đóng bao Jumbo 650kg & lưu kho (Cân đóng bao M-04)", Stage4TargetKg)
            };

            foreach (var (staffId, desc, target) in stages)
            {
                var assignment = new PersonnelAssignment
                {
                    StaffId = staffId > 0 ? staffId : currentUserId,
                    AssignedBy = currentUserId,
                    AssignmentDate = NewPlanDate,
                    Shift = NewPlanShift,
                    TaskDescription = desc,
                    Status = "Assigned"
                };
                _context.PersonnelAssignments.Add(assignment);
            }

            await _context.SaveChangesAsync();
            SuccessMessage = $"Đã lập thành công kế hoạch sản xuất ca {NewPlanShift} ngày {NewPlanDate:dd/MM/yyyy} với mục tiêu {NewPlanTargetKg:N0} kg.";
            return RedirectToPage();
        }

        public List<PersonnelAssignment> GetStagesForPlan(int planId)
        {
            var plan = Plans.FirstOrDefault(p => p.PlanId == planId);
            return plan?.Stages ?? new List<PersonnelAssignment>();
        }
    }
}
