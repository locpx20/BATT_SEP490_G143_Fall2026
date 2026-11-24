using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using BATT_PPMS.Pages.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Deliveries
{
    /// <summary>
    /// Screen: Chi tiết yêu cầu xuất kho. Thủ kho đối chiếu thông tin khi người mua đến:
    /// sai thì trả lại Manager sửa, đúng thì sang trang lập phiếu xuất kho. Manager chỉ khai báo tài xế/xe;
    /// số bao do thủ kho nhập khi lập phiếu, có thể lập nhiều phiếu cho đến khi đủ số bao của đơn.
    /// </summary>
    public class DetailModel : BasePageModel
    {
        private readonly ProductionManagementDbContext _context;

        public DetailModel(ProductionManagementDbContext context)
        {
            _context = context;
        }

        public Delivery Delivery { get; private set; } = null!;
        public Order Order => Delivery.Order;

        public int TotalOrderBags { get; private set; }
        public int ExportedOrderBags { get; private set; }
        public int PendingOrderBags { get; private set; }
        public int RemainingOrderBags { get; private set; }

        /// <summary>Kiểm tra lần đầu: xe chưa có phiếu nào → thủ kho xác nhận thông tin hoặc trả lại Manager.</summary>
        public bool CanCheck => IsWarehouseStaff && Delivery.DeliveryStatus == StatusConstants.Prepared
                                && Delivery.ExportSlips.Count == 0 && Order.Status == StatusConstants.Approved;

        /// <summary>Lập thêm phiếu cho xe này khi đơn còn bao chưa lập phiếu.</summary>
        public bool CanAddSlip => IsWarehouseStaff && Order.Status == StatusConstants.Approved
                                  && Delivery.DeliveryStatus is StatusConstants.Prepared or StatusConstants.Delivered
                                  && RemainingOrderBags > 0 && Delivery.ExportSlips.Count > 0;
        public bool CanEdit => IsManager && DeliveryWorkflow.CanEdit(Delivery);

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.WarehouseStaff, RoleConstants.Owner);
            if (check != null) return check;

            var delivery = await _context.Deliveries.AsNoTracking()
                .Include(d => d.CreatedByNavigation)
                .Include(d => d.ConfirmedByNavigation)
                .Include(d => d.ExportSlips).ThenInclude(s => s.Lines)
                .Include(d => d.Order).ThenInclude(o => o.Customer)
                .Include(d => d.Order).ThenInclude(o => o.OrderDetails).ThenInclude(x => x.Batch)
                .AsSplitQuery()
                .FirstOrDefaultAsync(d => d.DeliveryId == id);
            if (delivery == null) return NotFound();

            Delivery = delivery;

            var orderLines = await _context.ExportSlipLines.AsNoTracking()
                .Where(l => l.Slip.OrderId == delivery.OrderId)
                .Select(l => new { l.Bags, l.Slip.Status })
                .ToListAsync();
            TotalOrderBags = (int)Math.Round(delivery.Order.Quantity / (decimal)SalesFlow.KgPerBag);
            ExportedOrderBags = orderLines.Where(l => l.Status == ExportSlipStatus.Completed).Sum(l => l.Bags);
            PendingOrderBags = orderLines.Where(l => l.Status == ExportSlipStatus.Processing).Sum(l => l.Bags);
            RemainingOrderBags = Math.Max(0, TotalOrderBags - ExportedOrderBags - PendingOrderBags);
            return Page();
        }

        /// <summary>Thông tin sai: gửi lại Manager chỉnh sửa, bắt buộc ghi lý do.</summary>
        public async Task<IActionResult> OnPostReturnAsync(int id, string? reason)
        {
            var check = RequireRole(RoleConstants.WarehouseStaff);
            if (check != null) return check;

            var delivery = await _context.Deliveries
                .Include(d => d.ExportSlips)
                .Include(d => d.Order)
                .FirstOrDefaultAsync(d => d.DeliveryId == id);
            if (delivery == null) return NotFound();

            if (delivery.DeliveryStatus != StatusConstants.Prepared || delivery.ExportSlips.Count > 0)
                return Fail(id, "Yêu cầu xuất kho này không còn ở trạng thái chờ kiểm tra.");

            reason = reason?.Trim();
            if (string.IsNullOrEmpty(reason)) return Fail(id, "Vui lòng ghi rõ thông tin nào chưa đúng.");
            if (reason.Length > 500) return Fail(id, "Lý do không được vượt quá 500 ký tự.");

            delivery.DeliveryStatus = "Returned";
            delivery.ReturnReason = reason;
            delivery.UpdatedAt = DateTime.Now;

            await SalesFlow.NotifyUserOrRoleAsync(_context, delivery.CreatedBy ?? delivery.Order.CreatedBy, RoleConstants.Manager,
                "Yêu cầu xuất kho bị trả lại",
                $"{delivery.Order.OrderCode} cần chỉnh sửa: {reason}",
                $"/Deliveries/Detail?id={delivery.DeliveryId}");

            try { await _context.SaveChangesAsync(); }
            catch (DataValidationException ex) { return Fail(id, string.Join(" ", ex.Errors)); }

            SuccessMessage = $"Đã trả lại yêu cầu xuất kho {delivery.Order.OrderCode} cho Manager chỉnh sửa.";
            return RedirectToPage(new { id });
        }

        private IActionResult Fail(int id, string message)
        {
            ErrorMessage = message;
            return RedirectToPage(new { id });
        }
    }
}
