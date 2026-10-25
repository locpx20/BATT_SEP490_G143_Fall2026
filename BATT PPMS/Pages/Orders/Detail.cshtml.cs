using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    public class DetailModel : BasePageModel
    {
        private readonly AppDbContext _context;
        public DetailModel(AppDbContext context) { _context = context; }

        public Order? Order { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var check = RequireLogin();
            if (check != null) return check;

            Order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.InventoryItem)
                .Include(o => o.Creator)
                .Include(o => o.Approver)
                .Include(o => o.Delivery)
                .Include(o => o.QCInspection)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (Order == null) return NotFound();
            return Page();
        }

        // TODO: OnPostApproveAsync, OnPostRejectAsync
    }
}
