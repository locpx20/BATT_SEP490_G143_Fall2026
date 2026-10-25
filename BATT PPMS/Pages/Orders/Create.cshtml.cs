using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    public class CreateModel : BasePageModel
    {
        private readonly AppDbContext _context;
        public CreateModel(AppDbContext context) { _context = context; }

        [BindProperty]
        public Order Order { get; set; } = new();

        public List<Customer> Customers { get; set; } = new();
        public List<InventoryItem> Items { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireLogin();
            if (check != null) return check;

            Customers = await _context.Customers
                .Where(c => c.Status == StatusConstants.Active).ToListAsync();
            Items = await _context.InventoryItems
                .Where(i => i.Status == "Available").ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var check = RequireLogin();
            if (check != null) return check;

            // TODO: Validate, generate OrderCode, set TotalAmount, set CreatedBy
            Order.CreatedBy = CurrentUserId;
            Order.TotalAmount = Order.Quantity * Order.UnitPrice;
            Order.OrderCode = $"ORD-{DateTime.Now:yyyyMMddHHmmss}";
            Order.Status = StatusConstants.Pending;

            _context.Orders.Add(Order);
            await _context.SaveChangesAsync();

            return RedirectToPage("/Orders/Index");
        }
    }
}
