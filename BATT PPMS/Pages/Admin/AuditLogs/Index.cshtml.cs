using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Admin.AuditLogs
{
    /// <summary>
    /// Flat ViewModel matching the AuditLogView design from BATT_PPMSss.
    /// </summary>
    public class AuditLogEntry
    {
        public DateTime CreatedAt { get; set; }
        public string ActorName { get; set; } = string.Empty;
        public string ActorRole { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityCode { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? PreviousStatus { get; set; }
        public string? NewStatus { get; set; }
        public string Details { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;
        private const int PageSize = 15;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public List<AuditLogEntry> AuditLogs { get; set; } = new();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? EntityFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Page { get; set; } = 1;

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            CurrentPage = Math.Max(1, Page);

            var logs = new List<AuditLogEntry>();

            // 1. Orders (Xuất hàng)
            if (string.IsNullOrEmpty(EntityFilter) || EntityFilter == "Order")
            {
                var orders = await _context.Orders
                    .Include(o => o.Creator)
                    .Include(o => o.Approver)
                    .Include(o => o.Customer)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(30)
                    .ToListAsync();

                foreach (var o in orders)
                {
                    if (o.ApprovedBy.HasValue && o.Approver != null)
                    {
                        logs.Add(new AuditLogEntry
                        {
                            CreatedAt      = o.OrderDate.AddHours(2),
                            ActorName      = o.Approver.FullName,
                            ActorRole      = "Owner",
                            Action         = "Phê duyệt Lệnh xuất hàng",
                            EntityCode     = $"ORD-{o.OrderId:D4}",
                            EntityType     = "Order",
                            PreviousStatus = "PENDING",
                            NewStatus      = "APPROVED",
                            Details        = $"Phê duyệt xuất kho cho khách hàng: {o.Customer?.CustomerName ?? "N/A"}"
                        });
                    }

                    logs.Add(new AuditLogEntry
                    {
                        CreatedAt      = o.OrderDate,
                        ActorName      = o.Creator?.FullName ?? "Quản lý",
                        ActorRole      = "Manager",
                        Action         = "Khởi tạo Lệnh xuất hàng",
                        EntityCode     = $"ORD-{o.OrderId:D4}",
                        EntityType     = "Order",
                        PreviousStatus = null,
                        NewStatus      = "PENDING",
                        Details        = $"Tạo lệnh xuất khẩu viên nén gỗ cho {o.Customer?.CustomerName ?? "N/A"}"
                    });
                }
            }

            // 2. QC Inspections
            if (string.IsNullOrEmpty(EntityFilter) || EntityFilter == "QC")
            {
                var qcList = await _context.QCInspections
                    .Include(q => q.Requester)
                    .Include(q => q.Inspector)
                    .OrderByDescending(q => q.RequestDate)
                    .Take(20)
                    .ToListAsync();

                foreach (var q in qcList)
                {
                    if (q.Inspector != null && !string.IsNullOrEmpty(q.Result))
                    {
                        logs.Add(new AuditLogEntry
                        {
                            CreatedAt      = q.InspectDate ?? q.RequestDate,
                            ActorName      = q.Inspector.FullName,
                            ActorRole      = "QC Staff",
                            Action         = "Hoàn tất kiểm định KCS",
                            EntityCode     = string.IsNullOrEmpty(q.InspectionCode) ? $"QC-{q.InspectionId:D4}" : q.InspectionCode,
                            EntityType     = "QC",
                            PreviousStatus = "INSPECTING",
                            NewStatus      = q.Result.ToUpper(),
                            Details        = $"Kết quả kiểm định chất lượng: {q.Result}"
                        });
                    }
                }
            }

            // 3. Mua vật tư
            if (string.IsNullOrEmpty(EntityFilter) || EntityFilter == "Purchase")
            {
                var purchases = await _context.MaterialPurchaseRequests
                    .Include(p => p.Requester)
                    .OrderByDescending(p => p.RequestDate)
                    .Take(20)
                    .ToListAsync();

                foreach (var p in purchases)
                {
                    logs.Add(new AuditLogEntry
                    {
                        CreatedAt      = p.RequestDate,
                        ActorName      = p.Requester?.FullName ?? "NV Thu mua",
                        ActorRole      = "Purchasing Staff",
                        Action         = "Đề xuất Mua vật tư / NVL",
                        EntityCode     = $"PO-{p.RequestId:D4}",
                        EntityType     = "Purchase",
                        PreviousStatus = "DRAFT",
                        NewStatus      = p.Status.ToUpper(),
                        Details        = $"Yêu cầu cung ứng: {p.ItemName} (SL: {p.Quantity})"
                    });
                }
            }

            // 4. User management
            if (string.IsNullOrEmpty(EntityFilter) || EntityFilter == "User")
            {
                var users = await _context.Users
                    .Include(u => u.Role)
                    .Take(15)
                    .ToListAsync();

                foreach (var u in users)
                {
                    logs.Add(new AuditLogEntry
                    {
                        CreatedAt      = DateTime.Now.AddDays(-u.UserId),
                        ActorName      = "Admin System",
                        ActorRole      = "Admin",
                        Action         = u.Status == "Active" ? "Cấp tài khoản & Phân quyền" : "Khóa tài khoản",
                        EntityCode     = $"USR-{u.UserId:D4}",
                        EntityType     = "User",
                        PreviousStatus = u.Status == "Active" ? null : "ACTIVE",
                        NewStatus      = u.Status == "Active" ? "ACTIVE" : "LOCKED",
                        Details        = $"Phân quyền vai trò '{u.Role?.RoleName}' cho {u.FullName} ({u.Username})"
                    });
                }
            }

            // 5. Maintenance
            if (string.IsNullOrEmpty(EntityFilter) || EntityFilter == "Maintenance")
            {
                var maintenances = await _context.MaintenanceRequests
                    .Include(m => m.Requester)
                    .OrderByDescending(m => m.RequestDate)
                    .Take(15)
                    .ToListAsync();

                foreach (var m in maintenances)
                {
                    logs.Add(new AuditLogEntry
                    {
                        CreatedAt      = m.RequestDate,
                        ActorName      = m.Requester?.FullName ?? "Nhân viên",
                        ActorRole      = "Staff",
                        Action         = "Báo cáo Sự cố máy móc",
                        EntityCode     = $"MNT-{m.RequestId:D4}",
                        EntityType     = "Maintenance",
                        PreviousStatus = null,
                        NewStatus      = m.Status.ToUpper(),
                        Details        = m.Description ?? "Báo cáo sự cố thiết bị"
                    });
                }
            }

            // Search filter
            var filtered = logs.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var kw = SearchTerm.Trim().ToLower();
                filtered = filtered.Where(l =>
                    l.ActorName.ToLower().Contains(kw) ||
                    l.Action.ToLower().Contains(kw) ||
                    l.EntityCode.ToLower().Contains(kw) ||
                    l.Details.ToLower().Contains(kw));
            }

            var sorted = filtered.OrderByDescending(l => l.CreatedAt).ToList();
            TotalCount = sorted.Count;
            TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
            TotalPages = Math.Max(1, TotalPages);
            CurrentPage = Math.Min(CurrentPage, TotalPages);

            AuditLogs = sorted
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            return Page();
        }
    }
}
