using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Maintenance
{
    public class RepairRequestRow
    {
        public int RequestId { get; set; }
        public string Code => $"REQ-{RequestId:D4}";
        public string MachineCode { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Priority { get; set; }
        public string Status { get; set; } = RepairStatus.Pending;
        public string RequestedByName { get; set; } = string.Empty;
        public string? AssignedToName { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string? RejectionReason { get; set; }
        public bool MachineStopped { get; set; }
    }

    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? PriorityFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public List<RepairRequestRow> Rows { get; set; } = new();
        public PageWindow Pagination { get; private set; } = new(0, 1);

        // KPI (trong phạm vi người dùng được xem)
        public int TotalCount { get; set; }
        public int PendingCount { get; set; }
        public int RejectedCount { get; set; }
        public int AssignedCount { get; set; }
        public int InProgressCount { get; set; }
        public int WaitingMaterialCount { get; set; }
        public int CompletedCount { get; set; }

        // Chỉ nhân viên (Staff) tạo yêu cầu sửa chữa
        public bool CanCreate => IsStaff;

        public string ScopeHint =>
            IsStaff ? "Các yêu cầu sửa chữa do bạn tạo."
            : IsMaintenanceStaff ? "Các yêu cầu được phân công cho bạn."
            : "Toàn bộ yêu cầu sửa chữa trong nhà máy.";

        public List<AppNotification> Notices { get; private set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager, RoleConstants.MaintenanceStaff, RoleConstants.Staff);
            if (check != null) return check;

            PageNumber = PageWindow.ReadRequestedPage(Request.Query, PageNumber);

            // Phạm vi xem: Staff chỉ thấy yêu cầu của mình, Maintenance Staff chỉ thấy việc được giao
            IQueryable<MaintenanceRequest> scoped = _context.MaintenanceRequests.AsNoTracking();
            if (IsStaff)
                scoped = scoped.Where(r => r.RequestedBy == CurrentUserId);
            else if (IsMaintenanceStaff)
                scoped = scoped.Where(r => r.AssignedTo == CurrentUserId);

            var counts = await scoped
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();
            int Count(string status) => counts.Where(c => RepairStatus.Normalize(c.Status) == status).Sum(c => c.Count);

            TotalCount = counts.Sum(c => c.Count);
            PendingCount = Count(RepairStatus.Pending);
            RejectedCount = Count(RepairStatus.Rejected);
            AssignedCount = Count(RepairStatus.Assigned);
            InProgressCount = Count(RepairStatus.InProgress);
            WaitingMaterialCount = Count(RepairStatus.WaitingMaterial);
            Notices = await Notifier.TakeUnreadAsync(_context, CurrentUserId);
            CompletedCount = Count(RepairStatus.Completed);

            var query = scoped;

            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "ALL")
            {
                var statuses = RepairStatus.Equivalents(RepairStatus.Normalize(StatusFilter));
                query = query.Where(r => r.Status != null && statuses.Contains(r.Status));
            }

            if (!string.IsNullOrWhiteSpace(PriorityFilter) && PriorityFilter != "ALL")
            {
                var priority = PriorityFilter;
                query = query.Where(r => r.Priority == priority);
            }

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim();
                var idText = term.StartsWith("REQ-", StringComparison.OrdinalIgnoreCase) ? term.Substring(4) : term;
                var idValue = int.TryParse(idText, out var parsedId) ? parsedId : -1;

                query = query.Where(r =>
                    r.RequestId == idValue ||
                    (r.Title != null && r.Title.Contains(term)) ||
                    (r.Location != null && r.Location.Contains(term)) ||
                    r.BrokenReport.Machine.MachineName.Contains(term) ||
                    r.BrokenReport.Machine.MachineCode.Contains(term) ||
                    r.RequestedByNavigation.FullName.Contains(term) ||
                    (r.AssignedToNavigation != null && r.AssignedToNavigation.FullName.Contains(term)));
            }

            Pagination = new PageWindow(await query.CountAsync(), PageNumber);

            var items = await query
                .OrderByDescending(r => r.RequestDate)
                .ThenByDescending(r => r.RequestId)
                .Skip(Pagination.Offset)
                .Take(PageWindow.Size)
                .Select(r => new
                {
                    r.RequestId,
                    MachineCode = r.BrokenReport.Machine.MachineCode,
                    MachineName = r.BrokenReport.Machine.MachineName,
                    r.Location,
                    r.Title,
                    r.Description,
                    r.Priority,
                    r.Status,
                    RequestedByName = r.RequestedByNavigation.FullName,
                    AssignedToName = r.AssignedToNavigation != null ? r.AssignedToNavigation.FullName : null,
                    r.RequestDate,
                    r.DueDate,
                    r.RejectionReason,
                    r.MachineStopped
                })
                .ToListAsync();

            Rows = items.Select(r => new RepairRequestRow
            {
                RequestId = r.RequestId,
                MachineCode = r.MachineCode,
                MachineName = r.MachineName,
                Location = r.Location,
                Title = DisplayTitle(r.Title, r.Description),
                Priority = r.Priority,
                Status = RepairStatus.Normalize(r.Status),
                RequestedByName = r.RequestedByName,
                AssignedToName = r.AssignedToName,
                RequestDate = r.RequestDate,
                DueDate = r.DueDate,
                RejectionReason = r.RejectionReason ?? NoteTags.Get(r.Description, NoteTags.Rejected),
                MachineStopped = r.MachineStopped
            }).ToList();

            return Page();
        }

        /// <summary>Yêu cầu cũ chưa có tiêu đề: lấy dòng đầu của mô tả.</summary>
        public static string DisplayTitle(string? title, string? description)
        {
            if (!string.IsNullOrWhiteSpace(title)) return title;
            var plain = NoteTags.Plain(description);
            if (string.IsNullOrWhiteSpace(plain)) return "(Chưa có tiêu đề)";
            var firstLine = plain.Replace("\r", "").Split('\n')[0].Trim();
            return firstLine.Length > 90 ? firstLine.Substring(0, 90) + "…" : firstLine;
        }
    }
}
