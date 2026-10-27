using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    /// <summary>Screen: Update Outbound Order (Manager). Chỉ sửa được khi đơn còn Pending.</summary>
    public class EditModel : OrderFormPageModel
    {
        public EditModel(ProductionManagementDbContext context) : base(context) { }

        public override bool IsEdit => true;

        public int OrderId { get; private set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();

            if (!OrderWorkflow.CanModify(order.Status))
            {
                ErrorMessage = $"Đơn hàng {order.OrderCode} đang ở trạng thái \"{OrderWorkflow.Label(order.Status)}\" nên không thể chỉnh sửa.";
                return RedirectToPage("/Orders/Detail", new { id });
            }

            OrderId = order.OrderId;
            EditingOrderCode = order.OrderCode;
            Input = new OrderInput
            {
                CustomerId = order.CustomerId,
                InventoryId = order.InventoryId,
                Quantity = order.Quantity,
                UnitPrice = order.UnitPrice,
                OrderDate = order.OrderDate
            };

            await LoadLookupsAsync(order.CustomerId, order.InventoryId);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();

            if (!OrderWorkflow.CanModify(order.Status))
            {
                ErrorMessage = $"Đơn hàng {order.OrderCode} đã chuyển sang trạng thái \"{OrderWorkflow.Label(order.Status)}\" nên không thể chỉnh sửa.";
                return RedirectToPage("/Orders/Detail", new { id });
            }

            OrderId = order.OrderId;
            EditingOrderCode = order.OrderCode;

            await ValidateBusinessRulesAsync(order.CustomerId, order.InventoryId);

            if (!ModelState.IsValid)
            {
                await LoadLookupsAsync(order.CustomerId, order.InventoryId);
                return Page();
            }

            order.CustomerId = Input.CustomerId!.Value;
            order.InventoryId = Input.InventoryId!.Value;
            order.Quantity = Input.Quantity!.Value;
            order.UnitPrice = Input.UnitPrice!.Value;
            order.TotalAmount = Input.Quantity.Value * Input.UnitPrice.Value;
            order.OrderDate = Input.OrderDate!.Value;

            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã cập nhật đơn hàng {order.OrderCode}.";
            return RedirectToPage("/Orders/Detail", new { id });
        }
    }
}
