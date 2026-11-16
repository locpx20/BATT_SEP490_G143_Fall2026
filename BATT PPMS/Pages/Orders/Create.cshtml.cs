using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    /// <summary>Screen: Tạo yêu cầu bán hàng (Manager). Hệ thống tự phân bổ lô theo FIFO.</summary>
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

            await LoadLotsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var allocation = await AllocateAsync();
            if (allocation == null)
            {
                await LoadLotsAsync();
                return Page();
            }

            // Mã yêu cầu dạng ORD-yyyyMMdd-001; có UNIQUE index trên order_code nên thử lại nếu trùng do tạo đồng thời.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var codes = await _context.Orders.AsNoTracking().Select(o => o.OrderCode).ToListAsync();
                var order = new Order
                {
                    OrderCode = SalesFlow.NextCode(codes, "ORD"),
                    Grade = Input.Grade,
                    Quantity = Input.Bags!.Value * SalesFlow.KgPerBag,
                    UnitPrice = null,
                    TotalAmount = 0,
                    Note = string.IsNullOrWhiteSpace(Input.Note) ? null : Input.Note.Trim(),
                    OrderDate = DateTime.Now,
                    Status = StatusConstants.Pending,
                    CreatedBy = CurrentUserId
                };
                ApplyAllocation(order, allocation);

                _context.Orders.Add(order);
                try
                {
                    await _context.SaveChangesAsync();
                    await SalesFlow.NotifyRoleAsync(_context, RoleConstants.Owner,
                        "Yêu cầu bán hàng mới",
                        $"{order.OrderCode}: {Input.Bags:N0} bao {SalesFlow.GradeLabel(order.Grade)} chờ bạn duyệt, chọn người mua và giá bán.",
                        $"/Orders/Detail?id={order.OrderId}");
                    await _context.SaveChangesAsync();

                    SuccessMessage = $"Đã tạo yêu cầu bán {order.OrderCode} và gửi Owner phê duyệt.";
                    return RedirectToPage("/Orders/Detail", new { id = order.OrderId });
                }
                catch (DbUpdateException) when (attempt < 2)
                {
                    _context.ChangeTracker.Clear();
                }
                catch (DbUpdateException)
                {
                    _context.ChangeTracker.Clear();
                    ModelState.AddModelError(string.Empty, "Không thể lưu yêu cầu bán, vui lòng thử lại.");
                }
            }

            await LoadLotsAsync();
            return Page();
        }
    }
}
