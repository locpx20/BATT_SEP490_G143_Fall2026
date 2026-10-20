using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) { _context = context; }

        public List<Order> Orders { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireLogin();
            if (check != null) return check;

            Orders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.InventoryItem)
                .Include(o => o.Creator)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return Page();
        }
    }
}
