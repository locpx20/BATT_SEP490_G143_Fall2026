using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    /// <summary>Screen: Chi tiết yêu cầu bán. Owner duyệt (gán khách hàng + giá) hoặc từ chối (ghi lý do) tại đây.</summary>
    public class DetailModel : BasePageModel
    {
        private readonly ProductionManagementDbContext _context;

        public DetailModel(ProductionManagementDbContext context)
        {
            _context = context;
        }

        public Order Order { get; private set; } = null!;
        public Delivery? ActiveDelivery { get; private set; }
        public List<Customer> Customers { get; private set; } = new();

        public bool CanManage => IsManager;
        public bool CanModify => IsManager && OrderWorkflow.CanModify(Order.Status);
        public bool CanDecide => IsOwner && Order.Status == StatusConstants.Pending;
        public bool CanCreateDelivery => IsManager && Order.Status == StatusConstants.Approved && ActiveDelivery == null;
        public bool CanEditDelivery => IsManager && ActiveDelivery != null && DeliveryWorkflow.CanEdit(ActiveDelivery);

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            if (!await LoadAsync(id)) return NotFound();

            if (CanDecide)
                Customers = await _context.Customers.AsNoTracking()
                    .Where(c => c.Status == StatusConstants.Active)
                    .OrderBy(c => c.CustomerName)
                    .ToListAsync();
            return Page();
        }

        /// <summary>Owner đồng ý: cập nhật người mua + giá bán rồi gửi lại cho Manager.</summary>
        public async Task<IActionResult> OnPostApproveAsync(int id, int? customerId, decimal? unitPrice)
        {
            var check = RequireRole(RoleConstants.Owner);
            if (check != null) return check;

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();
            if (order.Status != StatusConstants.Pending)
                return Fail(id, $"Yêu cầu {order.OrderCode} không còn ở trạng thái chờ duyệt.");

            if (customerId == null) return Fail(id, "Vui lòng chọn người mua.");
            var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == customerId);
            if (customer == null || customer.Status != StatusConstants.Active)
                return Fail(id, "Khách hàng không tồn tại hoặc đã ngưng hoạt động.");

            if (unitPrice == null || unitPrice <= 0) return Fail(id, "Giá bán phải lớn hơn 0.");
            var price = Math.Round(unitPrice.Value, 2);
            var total = Math.Round(order.Quantity * price, 2);
            if (price > OrderWorkflow.MaxAmount || total > OrderWorkflow.MaxAmount)
                return Fail(id, "Giá bán quá lớn: tổng tiền vượt giới hạn cho phép.");

            order.CustomerId = customer.CustomerId;
            order.UnitPrice = price;
            order.TotalAmount = total;
            order.Status = StatusConstants.Approved;
            order.RejectionReason = null;
            order.ApprovedBy = CurrentUserId;
            order.ApprovedAt = DateTime.Now;

            await SalesFlow.NotifyUserOrRoleAsync(_context, order.CreatedBy, RoleConstants.Manager,
                "Yêu cầu bán đã được duyệt",
                $"{order.OrderCode}: người mua {customer.CustomerName}, giá {price:N0} ₫/kg. Hãy tạo yêu cầu xuất kho.",
                $"/Orders/Detail?id={order.OrderId}");

            try { await _context.SaveChangesAsync(); }
            catch (DataValidationException ex) { return Fail(id, string.Join(" ", ex.Errors)); }

            SuccessMessage = $"Đã duyệt yêu cầu {order.OrderCode} và gửi lại cho Manager.";
            return RedirectToPage(new { id });
        }

        /// <summary>Owner không đồng ý: bắt buộc ghi lý do.</summary>
        public async Task<IActionResult> OnPostRejectAsync(int id, string? reason)
        {
            var check = RequireRole(RoleConstants.Owner);
            if (check != null) return check;

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null) return NotFound();
            if (order.Status != StatusConstants.Pending)
                return Fail(id, $"Yêu cầu {order.OrderCode} không còn ở trạng thái chờ duyệt.");

            reason = reason?.Trim();
            if (string.IsNullOrEmpty(reason)) return Fail(id, "Vui lòng ghi lý do từ chối.");
            if (reason.Length > 1000) return Fail(id, "Lý do từ chối không được vượt quá 1000 ký tự.");

            order.Status = StatusConstants.Rejected;
            order.RejectionReason = reason;
            order.ApprovedBy = CurrentUserId;
            order.ApprovedAt = DateTime.Now;

            await SalesFlow.NotifyUserOrRoleAsync(_context, order.CreatedBy, RoleConstants.Manager,
                "Yêu cầu bán bị từ chối",
                $"{order.OrderCode} bị từ chối: {reason}",
                $"/Orders/Detail?id={order.OrderId}");

            try { await _context.SaveChangesAsync(); }
            catch (DataValidationException ex) { return Fail(id, string.Join(" ", ex.Errors)); }

            SuccessMessage = $"Đã từ chối yêu cầu {order.OrderCode}.";
            return RedirectToPage(new { id });
        }

        private IActionResult Fail(int id, string message)
        {
            ErrorMessage = message;
            return RedirectToPage(new { id });
        }

        private async Task<bool> LoadAsync(int id)
        {
            var order = await _context.Orders.AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.CreatedByNavigation)
                .Include(o => o.ApprovedByNavigation)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Batch)
                .Include(o => o.Deliveries).ThenInclude(d => d.ExportSlip)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return false;

            Order = order;
            ActiveDelivery = OrderWorkflow.ActiveDelivery(order);
            return true;
        }
    }
}
