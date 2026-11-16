using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    /// <summary>
    /// Screen: Sửa yêu cầu bán (Manager). Sửa được khi Chờ duyệt hoặc bị Owner từ chối;
    /// lưu xong yêu cầu quay về Chờ duyệt và lô được phân bổ lại theo FIFO.
    /// </summary>
    public class EditModel : OrderFormPageModel
    {
        public EditModel(ProductionManagementDbContext context) : base(context) { }

        public override bool IsEdit => true;

        public int OrderId { get; private set; }

        public string? RejectionReason { get; private set; }

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
                ErrorMessage = $"Yêu cầu {order.OrderCode} đang ở trạng thái \"{OrderWorkflow.Label(order.Status)}\" nên không thể chỉnh sửa.";
                return RedirectToPage("/Orders/Detail", new { id });
            }

            OrderId = order.OrderId;
            EditingOrderCode = order.OrderCode;
            RejectionReason = order.RejectionReason;
            Input = new OrderInput
            {
                Grade = order.Grade,
                Bags = order.Quantity / SalesFlow.KgPerBag,
                Note = order.Note
            };

            await LoadLotsAsync(order.OrderId);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();

            if (!OrderWorkflow.CanModify(order.Status))
            {
                ErrorMessage = $"Yêu cầu {order.OrderCode} đã chuyển sang trạng thái \"{OrderWorkflow.Label(order.Status)}\" nên không thể chỉnh sửa.";
                return RedirectToPage("/Orders/Detail", new { id });
            }

            OrderId = order.OrderId;
            EditingOrderCode = order.OrderCode;
            RejectionReason = order.RejectionReason;

            var allocation = await AllocateAsync(order.OrderId);
            if (allocation == null)
            {
                await LoadLotsAsync(order.OrderId);
                return Page();
            }

            var wasRejected = order.Status == StatusConstants.Rejected;

            _context.OrderDetails.RemoveRange(order.OrderDetails);
            ApplyAllocation(order, allocation);

            order.Grade = Input.Grade;
            order.Quantity = Input.Bags!.Value * SalesFlow.KgPerBag;
            order.Note = string.IsNullOrWhiteSpace(Input.Note) ? null : Input.Note.Trim();
            order.Status = StatusConstants.Pending;
            order.RejectionReason = null;
            order.ApprovedBy = null;
            order.ApprovedAt = null;
            order.CustomerId = null;
            order.UnitPrice = null;
            order.TotalAmount = 0;

            await SalesFlow.NotifyRoleAsync(_context, RoleConstants.Owner,
                wasRejected ? "Yêu cầu bán được gửi lại" : "Yêu cầu bán đã được chỉnh sửa",
                $"{order.OrderCode}: {Input.Bags:N0} bao {SalesFlow.GradeLabel(order.Grade)} chờ bạn duyệt.",
                $"/Orders/Detail?id={order.OrderId}");

            await _context.SaveChangesAsync();

            SuccessMessage = wasRejected
                ? $"Đã cập nhật và gửi lại yêu cầu {order.OrderCode} cho Owner."
                : $"Đã cập nhật yêu cầu {order.OrderCode}.";
            return RedirectToPage("/Orders/Detail", new { id });
        }
    }
}
