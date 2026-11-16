using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    /// <summary>Screen: Hủy yêu cầu bán (Manager). Chỉ hủy được yêu cầu Chờ duyệt / bị từ chối; lô được giải phóng.</summary>
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

            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();

            if (!OrderWorkflow.CanModify(order.Status))
            {
                ErrorMessage = $"Yêu cầu {order.OrderCode} đang ở trạng thái \"{OrderWorkflow.Label(order.Status)}\" nên không thể hủy.";
                return RedirectToPage("/Orders/Detail", new { id });
            }

            Order = order;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null)
            {
                ErrorMessage = "Yêu cầu không còn tồn tại.";
                return RedirectToPage("/Orders/Index");
            }

            if (!OrderWorkflow.CanModify(order.Status))
            {
                ErrorMessage = $"Yêu cầu {order.OrderCode} đã chuyển sang trạng thái \"{OrderWorkflow.Label(order.Status)}\" nên không thể hủy.";
                return RedirectToPage("/Orders/Detail", new { id });
            }

            // Giữ lại lịch sử: chuyển sang Cancelled; trạng thái này không còn giữ chỗ tồn kho.
            var wasPending = order.Status == StatusConstants.Pending;
            order.Status = StatusConstants.Cancelled;
            order.CustomerId = null;
            order.UnitPrice = null;
            order.TotalAmount = 0;

            if (wasPending)
                await SalesFlow.NotifyRoleAsync(_context, RoleConstants.Owner, "Yêu cầu bán đã bị hủy",
                    $"{order.OrderCode} đã được Manager hủy.", $"/Orders/Detail?id={order.OrderId}");

            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã hủy yêu cầu {order.OrderCode}.";
            return RedirectToPage("/Orders/Index");
        }
    }
}
