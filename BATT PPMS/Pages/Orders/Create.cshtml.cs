using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    /// <summary>Screen: Create Outbound Order (Manager).</summary>
    public class CreateModel : OrderFormPageModel
    {
        public CreateModel(ProductionManagementDbContext context) : base(context) { }

        public override bool IsEdit => false;

        [TempData]
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var now = DateTime.Now;
            Input.OrderDate = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

            await LoadLookupsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            await ValidateBusinessRulesAsync();

            if (!ModelState.IsValid)
            {
                await LoadLookupsAsync();
                return Page();
            }

            // Mã đơn dạng ORD-yyyyMMdd-001; có UNIQUE index trên order_code nên thử lại nếu trùng do tạo đồng thời.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var order = new Order
                {
                    OrderCode = await NextOrderCodeAsync(),
                    CustomerId = Input.CustomerId!.Value,
                    InventoryId = Input.InventoryId!.Value,
                    Quantity = Input.Quantity!.Value,
                    UnitPrice = Input.UnitPrice!.Value,
                    TotalAmount = Input.Quantity.Value * Input.UnitPrice.Value,
                    OrderDate = Input.OrderDate!.Value,
                    Status = StatusConstants.Pending,
                    CreatedBy = CurrentUserId
                };

                _context.Orders.Add(order);
                try
                {
                    await _context.SaveChangesAsync();
                    SuccessMessage = $"Đã tạo đơn hàng {order.OrderCode} thành công (trạng thái: Chờ duyệt).";
                    return RedirectToPage("/Orders/Detail", new { id = order.OrderId });
                }
                catch (DbUpdateException) when (attempt < 2)
                {
                    _context.Entry(order).State = EntityState.Detached;
                }
                catch (DbUpdateException)
                {
                    _context.Entry(order).State = EntityState.Detached;
                    ModelState.AddModelError(string.Empty, "Không thể lưu đơn hàng, vui lòng thử lại.");
                }
            }

            await LoadLookupsAsync();
            return Page();
        }

        private async Task<string> NextOrderCodeAsync()
        {
            var prefix = $"ORD-{DateTime.Now:yyyyMMdd}-";
            var codes = await _context.Orders.AsNoTracking()
                .Where(o => o.OrderCode.StartsWith(prefix))
                .Select(o => o.OrderCode)
                .ToListAsync();

            var max = codes
                .Select(c => int.TryParse(c.Substring(prefix.Length), out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();

            return $"{prefix}{max + 1:D3}";
        }
    }
}
