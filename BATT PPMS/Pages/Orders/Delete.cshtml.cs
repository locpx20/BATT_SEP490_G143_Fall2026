using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    /// <summary>Screen: Delete Outbound Order (Manager). Trang xác nhận; chỉ xóa được đơn Pending.</summary>
    public class DeleteModel : BasePageModel
    {
        private readonly ProductionManagementDbContext _context;

        public DeleteModel(ProductionManagementDbContext context)
        {
            _context = context;
        }

        public Order Order { get; private set; } = null!;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var order = await LoadAsync(id, tracking: false);
            if (order == null) return NotFound();

            if (!OrderWorkflow.CanModify(order.Status))
            {
                ErrorMessage = $"Đơn hàng {order.OrderCode} đang ở trạng thái \"{OrderWorkflow.Label(order.Status)}\" nên không thể xóa.";
                return RedirectToPage("/Orders/Detail", new { id });
            }

            Order = order;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var order = await LoadAsync(id, tracking: true);
            if (order == null)
            {
                ErrorMessage = "Đơn hàng không còn tồn tại.";
                return RedirectToPage("/Orders/Index");
            }

            if (!OrderWorkflow.CanModify(order.Status))
            {
                ErrorMessage = $"Đơn hàng {order.OrderCode} đã chuyển sang trạng thái \"{OrderWorkflow.Label(order.Status)}\" nên không thể xóa.";
                return RedirectToPage("/Orders/Detail", new { id });
            }

            // Đơn Pending không nên có phân bổ bao hoặc giao hàng; nếu có thì chặn để không mất dữ liệu liên quan.
            if (order.OrderDetails.Count > 0 || order.Deliveries.Count > 0)
            {
                ErrorMessage = $"Đơn hàng {order.OrderCode} đã có dữ liệu xuất kho / giao hàng liên quan nên không thể xóa.";
                return RedirectToPage("/Orders/Detail", new { id });
            }

            var code = order.OrderCode;
            _context.Orders.Remove(order);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ErrorMessage = $"Không thể xóa đơn hàng {code} vì còn dữ liệu liên quan.";
                return RedirectToPage("/Orders/Detail", new { id });
            }

            SuccessMessage = $"Đã xóa đơn hàng {code}.";
            return RedirectToPage("/Orders/Index");
        }

        private Task<Order?> LoadAsync(int id, bool tracking)
        {
            var q = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Inventory)
                .Include(o => o.OrderDetails)
                .Include(o => o.Deliveries)
                .AsSplitQuery();

            if (!tracking) q = q.AsNoTracking();
            return q.FirstOrDefaultAsync(o => o.OrderId == id);
        }
    }
}
