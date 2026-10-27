using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Dashboard
{
    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public bool IsAdminRole { get; set; }

        // Admin Overview Stats
        public int TotalAccounts { get; set; }
        public int ActiveAccounts { get; set; }
        public int LockedAccounts { get; set; }
        public List<UserAuditSummaryItem> RecentUserLogs { get; set; } = new();

        // Operational stats (Owner / Manager / other roles)
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public int TodayDeliveries { get; set; }
        public int PendingQC { get; set; }
        public int BrokenEquipment { get; set; }
        public int LowStockItems { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireLogin();
            if (check != null) return check;

            var roleName = HttpContext.Session.GetString("RoleName") ?? "";
            IsAdminRole = roleName == RoleConstants.Admin || roleName == "Admin";

            if (IsAdminRole)
            {
                TotalAccounts = await _context.Users.CountAsync();
                ActiveAccounts = await _context.Users.CountAsync(u => u.Status == "Active");
                LockedAccounts = await _context.Users.CountAsync(u => u.Status != "Active");

                var users = await _context.Users
                    .Include(u => u.Role)
                    .OrderByDescending(u => u.UserId)
                    .Take(6)
                    .ToListAsync();

                RecentUserLogs = users.Select(u => new UserAuditSummaryItem
                {
                    CreatedAt = DateTime.Now.AddDays(-u.UserId),
                    Action = u.Status == "Active" ? "Cấp tài khoản & Phân quyền" : "Khóa tài khoản",
                    TargetUser = $"{u.FullName} ({u.Username})",
                    RoleName = u.Role?.RoleName ?? "Chưa phân vai trò",
                    Status = u.Status,
                    Details = $"Phân quyền vai trò '{u.Role?.RoleName}' cho tài khoản {u.Username}"
                }).ToList();
            }
            else
            {
                TotalOrders = await _context.Orders
                    .CountAsync(o => o.OrderDate.Date == DateTime.Today);

                PendingOrders = await _context.Orders
                    .CountAsync(o => o.Status == StatusConstants.Pending);

                TodayDeliveries = await _context.Deliveries
                    .CountAsync(d => d.PickupDate.Date == DateTime.Today);

                PendingQC = await _context.QCInspections
                    .CountAsync(q => q.Status == StatusConstants.Pending);

                BrokenEquipment = await _context.BrokenEquipmentReports
                    .CountAsync(b => b.Status == StatusConstants.Reported
                                  || b.Status == StatusConstants.UnderMaintenance);

                LowStockItems = await _context.InventoryItems
                    .CountAsync(i => i.Quantity <= i.MinQuantity);
            }

            return Page();
        }
    }

    public class UserAuditSummaryItem
    {
        public DateTime CreatedAt { get; set; }
        public string Action { get; set; } = string.Empty;
        public string TargetUser { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}
