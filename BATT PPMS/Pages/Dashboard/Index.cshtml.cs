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
        public bool IsProductionStaffRole { get; set; }
        public bool IsOwnerOrManagerRole { get; set; }

        // Admin Overview Stats
        public int TotalAccounts { get; set; }
        public int ActiveAccounts { get; set; }
        public int LockedAccounts { get; set; }
        public List<UserAuditSummaryItem> RecentUserLogs { get; set; } = new();

        // Operational stats (Matching BATT_PPMSss DashboardView)
        public decimal RawInventoryTons { get; set; } = 46.8m;
        public decimal FinishedInventoryTons { get; set; } = 10.0m;
        public decimal FinishedInventoryWarehouseA_Tons { get; set; } = 10.0m;
        public decimal FinishedInventoryWarehouseB_Tons { get; set; } = 0.0m;
        public int InProgressPlansCount { get; set; }
        public int ActiveFailuresCount { get; set; }
        public int PendingShiftReportsCount { get; set; }
        public int MyAssignedPlansCount { get; set; }
        public decimal RevenueMillionVnd { get; set; } = 38.5m;
        public decimal RemainingDebtMillionVnd { get; set; } = 18.5m;

        public List<OperationalTaskItem> PendingTasks { get; set; } = new();
        public List<StockMovementItem> RecentMovements { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireLogin();
            if (check != null) return check;

            var roleName = HttpContext.Session.GetString("RoleName") ?? "";
            IsAdminRole = roleName == RoleConstants.Admin || roleName == "Admin";
            IsProductionStaffRole = roleName == RoleConstants.Staff || roleName == "Staff";
            IsOwnerOrManagerRole = roleName == RoleConstants.Owner || roleName == RoleConstants.Manager;

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
                // In-progress plans count
                var distinctPlans = await _context.PersonnelAssignments
                    .Where(p => p.Status == "InProgress")
                    .GroupBy(p => new { p.AssignmentDate, p.Shift })
                    .CountAsync();
                InProgressPlansCount = distinctPlans > 0 ? distinctPlans : 1;

                // Active machine failures count
                ActiveFailuresCount = await _context.BrokenEquipmentReports
                    .CountAsync(b => b.Status != "Fixed" && b.Status != "Resolved" && b.Status != "Completed");

                // Pending shift reports count
                PendingShiftReportsCount = await _context.EndOfShiftReports
                    .CountAsync(r => r.Status == "Submitted" || r.Status == "Pending");

                // Staff personal assigned plans
                var currentUserId = CurrentUserId > 0 ? CurrentUserId : 4;
                MyAssignedPlansCount = await _context.PersonnelAssignments
                    .CountAsync(p => p.StaffId == currentUserId && p.Status != "Completed");

                // Build Pending / Work Tasks Queue
                PendingTasks = new List<OperationalTaskItem>();

                if (IsProductionStaffRole)
                {
                    if (InProgressPlansCount > 0)
                    {
                        PendingTasks.Add(new OperationalTaskItem
                        {
                            Title = $"{InProgressPlansCount} Kế hoạch sản xuất ca đang vận hành",
                            Description = "Vận hành dây chuyền băm nghiền, sấy mùn cưa và ép viên nén",
                            ActionText = "Xem lịch ca →",
                            ActionUrl = "/Personnel/Index",
                            BadgeColor = "sky"
                        });
                    }

                    PendingTasks.Add(new OperationalTaskItem
                    {
                        Title = "Gửi báo cáo nghiệm thu sản lượng ca làm việc",
                        Description = "Ghi nhận NVL thực dùng, sản lượng và phát hành mã LOT cho phòng KCS",
                        ActionText = "Nộp báo cáo →",
                        ActionUrl = "/ShiftReports/Index",
                        BadgeColor = "indigo"
                    });

                    if (ActiveFailuresCount > 0)
                    {
                        PendingTasks.Add(new OperationalTaskItem
                        {
                            Title = $"{ActiveFailuresCount} Sự cố máy móc cần theo dõi sửa chữa",
                            Description = "Kiểm tra tiến độ kỹ thuật viên cơ điện xử lý máy móc dây chuyền",
                            ActionText = "Kiểm tra →",
                            ActionUrl = "/Maintenance/Index",
                            BadgeColor = "rose"
                        });
                    }
                }
                else
                {
                    // Owner / Manager view
                    if (PendingShiftReportsCount > 0)
                    {
                        PendingTasks.Add(new OperationalTaskItem
                        {
                            Title = $"{PendingShiftReportsCount} Báo cáo ca sản xuất chờ duyệt",
                            Description = "Duyệt sản lượng để phát hành mã LOT cho phòng KCS",
                            ActionText = "Xem ngay →",
                            ActionUrl = "/ShiftReports/Index",
                            BadgeColor = "indigo"
                        });
                    }

                    PendingTasks.Add(new OperationalTaskItem
                    {
                        Title = "1 Phiếu thu mua đã cân chờ duyệt",
                        Description = "Cần kiểm tra phiếu cân và duyệt đơn giá thu mua",
                        ActionText = "Xem ngay →",
                        ActionUrl = "/PurchaseInvoices/Index",
                        BadgeColor = "amber"
                    });

                    PendingTasks.Add(new OperationalTaskItem
                    {
                        Title = "1 Đề xuất xuất hàng chờ Owner quyết định",
                        Description = "Quyết định giá bán và duyệt xuất hàng cho đối tác",
                        ActionText = "Quyết định →",
                        ActionUrl = "/Orders/Index",
                        BadgeColor = "emerald"
                    });

                    if (ActiveFailuresCount > 0)
                    {
                        PendingTasks.Add(new OperationalTaskItem
                        {
                            Title = $"{ActiveFailuresCount} Sự cố máy móc chờ phân công",
                            Description = "Chỉ định kỹ thuật viên phụ trách xử lý sự cố thiết bị",
                            ActionText = "Phân công →",
                            ActionUrl = "/Maintenance/Index",
                            BadgeColor = "rose"
                        });
                    }
                }

                // Recent Physical Stock Movements
                RecentMovements = new List<StockMovementItem>
                {
                    new StockMovementItem
                    {
                        Type = "IN",
                        QuantityKg = 24750,
                        RefCode = "PO-2026-001",
                        Time = "18:30",
                        Notes = "Nhập kho thực tế từ xe cân PO-2026-001",
                        ConfirmedBy = "Phạm Thị Phương Thảo (Thủ kho)"
                    },
                    new StockMovementItem
                    {
                        Type = "OUT",
                        QuantityKg = 22500,
                        RefCode = "MAT-ISS-001",
                        Time = "13:15",
                        Notes = "Cấp NVL cho Kế hoạch sản xuất PLAN-2026-042",
                        ConfirmedBy = "Phạm Thị Phương Thảo (Thủ kho)"
                    },
                    new StockMovementItem
                    {
                        Type = "IN",
                        QuantityKg = 20000,
                        RefCode = "LOT-20261024-01",
                        Time = "22:00",
                        Notes = "Nhập 20 bao Jumbo LOT-20261024-01 vào Kho A",
                        ConfirmedBy = "Phạm Thị Phương Thảo (Thủ kho)"
                    },
                    new StockMovementItem
                    {
                        Type = "OUT",
                        QuantityKg = 10000,
                        RefCode = "OUT-PROP-2026-015",
                        Time = "23:30",
                        Notes = "Xuất kho từ Kho A giao khách hàng Sumitomo (Xe 72C-184.92)",
                        ConfirmedBy = "Phạm Thị Phương Thảo (Thủ kho)"
                    }
                };
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

    public class OperationalTaskItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ActionText { get; set; } = string.Empty;
        public string ActionUrl { get; set; } = string.Empty;
        public string BadgeColor { get; set; } = "indigo";
    }

    public class StockMovementItem
    {
        public string Type { get; set; } = "IN";
        public int QuantityKg { get; set; }
        public string RefCode { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string ConfirmedBy { get; set; } = string.Empty;
    }
}
