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

        // Stats
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

            var today = DateOnly.FromDateTime(DateTime.Today);

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

            return Page();
        }
    }
}
