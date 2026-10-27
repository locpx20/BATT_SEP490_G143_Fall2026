using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    /// <summary>Screen: Order Detail (Manager).</summary>
    public class DetailModel : BasePageModel
    {
        private readonly ProductionManagementDbContext _context;

        public DetailModel(ProductionManagementDbContext context)
        {
            _context = context;
        }

        public Order Order { get; private set; } = null!;

        public bool CanManage => IsManager;
        public bool CanModify => IsManager && OrderWorkflow.CanModify(Order.Status);

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            var order = await _context.Orders.AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.Inventory)
                .Include(o => o.CreatedByNavigation)
                .Include(o => o.ApprovedByNavigation)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Batch)
                .Include(o => o.Deliveries)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();

            Order = order;
            return Page();
        }
    }
}
