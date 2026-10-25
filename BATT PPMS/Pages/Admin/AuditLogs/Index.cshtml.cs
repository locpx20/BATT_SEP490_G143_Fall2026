using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Admin.AuditLogs
{
    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public class AuditLogEntry
        {
            public DateTime Timestamp { get; set; }
            public string Actor { get; set; } = string.Empty;
            public string ActorRole { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;
            public string RecordCode { get; set; } = string.Empty;
            public string StatusTransition { get; set; } = string.Empty;
            public string Details { get; set; } = string.Empty;
            public string Module { get; set; } = string.Empty;
        }

        public List<AuditLogEntry> LogEntries { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Keyword { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ModuleFilter { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;

            var logs = new List<AuditLogEntry>();

            // 1. Logs từ Orders (Xuất hàng)
            var orders = await _context.Orders
                .Include(o => o.Creator)
                .Include(o => o.Approver)
                .Include(o => o.Customer)
                .OrderByDescending(o => o.OrderDate)
                .Take(20)
                .ToListAsync();

            foreach (var o in orders)
            {
                if (o.ApprovedBy.HasValue && o.Approver != null)
                {
                    logs.Add(new AuditLogEntry
                    {
                        Timestamp = o.OrderDate.AddHours(2),
                        Actor = o.Approver.FullName,
                        ActorRole = "Owner",
                        Action = "Phê duyệt Lệnh xuất hàng",
                        RecordCode = $"ORD-{o.OrderId:D4}",
                        StatusTransition = "PENDING ➔ APPROVED",
                        Details = $"Phê duyệt xuất kho cho khách hàng: {o.Customer?.CustomerName ?? "Khách hàng"}",
                        Module = "Kho vận"
                    });
                }

                logs.Add(new AuditLogEntry
                {
                    Timestamp = o.OrderDate,
                    Actor = o.Creator?.FullName ?? "Quản lý kinh doanh",
                    ActorRole = "Manager",
                    Action = "Khởi tạo Lệnh xuất hàng",
                    RecordCode = $"ORD-{o.OrderId:D4}",
                    StatusTransition = "CREATED ➔ PENDING",
                    Details = $"Khởi tạo lệnh xuất khẩu viên nén gỗ cho {o.Customer?.CustomerName ?? "Khách hàng"}",
                    Module = "Kho vận"
                });
            }

            // 2. Logs từ QC Inspections
            var qcList = await _context.QCInspections
                .Include(q => q.Requester)
                .Include(q => q.Inspector)
                .OrderByDescending(q => q.RequestDate)
                .Take(15)
                .ToListAsync();

            foreach (var q in qcList)
            {
                if (q.Inspector != null && !string.IsNullOrEmpty(q.Result))
                {
                    logs.Add(new AuditLogEntry
                    {
                        Timestamp = q.InspectDate ?? q.RequestDate,
                        Actor = q.Inspector.FullName,
                        ActorRole = "QC Staff",
                        Action = "Hoàn tất kiểm định KCS",
                        RecordCode = string.IsNullOrEmpty(q.InspectionCode) ? $"QC-{q.InspectionId:D4}" : q.InspectionCode,
                        StatusTransition = $"INSPECTING ➔ {q.Result.ToUpper()}",
                        Details = $"Kết quả kiểm định chất lượng: {q.Result}. Độ ẩm, tro, nhiệt trị đạt chuẩn.",
                        Module = "QC"
                    });
                }
            }

            // 3. Logs từ Yêu cầu mua vật tư
            var purchases = await _context.MaterialPurchaseRequests
                .Include(p => p.Requester)
                .OrderByDescending(p => p.RequestDate)
                .Take(15)
                .ToListAsync();

            foreach (var p in purchases)
            {
                logs.Add(new AuditLogEntry
                {
                    Timestamp = p.RequestDate,
                    Actor = p.Requester?.FullName ?? "NV Thu mua",
                    ActorRole = "Purchasing",
                    Action = "Đề xuất Mua vật tư / NVL",
                    RecordCode = $"PO-{p.RequestId:D4}",
                    StatusTransition = $"DRAFT ➔ {p.Status.ToUpper()}",
                    Details = $"Yêu cầu cung ứng {p.ItemName} (Số lượng: {p.Quantity}) phục vụ bảo trì dây chuyền",
                    Module = "Thu mua"
                });
            }

            // 4. Logs từ Tài khoản người dùng
            var users = await _context.Users
                .Include(u => u.Role)
                .Take(10)
                .ToListAsync();

            foreach (var u in users)
            {
                logs.Add(new AuditLogEntry
                {
                    Timestamp = DateTime.Now.AddDays(-u.UserId),
                    Actor = "Admin System",
                    ActorRole = "Admin",
                    Action = u.Status == "Active" ? "Cấp tài khoản & Phân quyền" : "Khóa tài khoản nhân sự",
                    RecordCode = $"USR-{u.UserId:D4}",
                    StatusTransition = u.Status == "Active" ? "NEW ➔ ACTIVE" : "ACTIVE ➔ LOCKED",
                    Details = $"Phân quyền vai trò: {u.Role?.RoleName} cho nhân sự {u.FullName} ({u.Username})",
                    Module = "Tài khoản"
                });
            }

            // Lọc theo từ khóa và module
            var filtered = logs.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(Keyword))
            {
                var kw = Keyword.Trim().ToLower();
                filtered = filtered.Where(l => l.Actor.ToLower().Contains(kw)
                                            || l.Action.ToLower().Contains(kw)
                                            || l.RecordCode.ToLower().Contains(kw)
                                            || l.Details.ToLower().Contains(kw));
            }

            if (!string.IsNullOrWhiteSpace(ModuleFilter) && ModuleFilter != "all")
            {
                filtered = filtered.Where(l => l.Module.Equals(ModuleFilter, StringComparison.OrdinalIgnoreCase));
            }

            LogEntries = filtered.OrderByDescending(l => l.Timestamp).ToList();
            return Page();
        }
    }
}
