using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using BATT_PPMS.Pages.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Deliveries
{
    public class DeliveryInput
    {
        [Required(ErrorMessage = "Vui lòng nhập tên tài xế.")]
        [StringLength(100, ErrorMessage = "Tên tài xế không được vượt quá 100 ký tự.")]
        public string? DriverName { get; set; }

        [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự.")]
        public string? DriverPhone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập biển số xe.")]
        [StringLength(20, ErrorMessage = "Biển số xe không được vượt quá 20 ký tự.")]
        public string? LicensePlate { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày lấy hàng.")]
        public DateTime? PickupDate { get; set; }

        [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
        public string? Note { get; set; }
    }

    /// <summary>Screen: Tạo / cập nhật yêu cầu xuất kho (Manager). Mở bằng ?orderId= để tạo, ?id= để sửa.</summary>
    public class EditModel : BasePageModel
    {
        private static readonly Regex PhonePattern = new(@"^[0-9+\-\s().]{8,20}$", RegexOptions.Compiled);
        private static readonly Regex PlatePattern = new(@"^[A-Za-z0-9\-\.\s]{4,20}$", RegexOptions.Compiled);

        private readonly ProductionManagementDbContext _context;

        public EditModel(ProductionManagementDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public DeliveryInput Input { get; set; } = new();

        public Order Order { get; private set; } = null!;
        public int? DeliveryId { get; private set; }
        public string? ReturnReason { get; private set; }
        public bool IsEdit => DeliveryId.HasValue;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id, int? orderId)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            if (id.HasValue)
            {
                var delivery = await _context.Deliveries.AsNoTracking()
                    .Include(d => d.ExportSlip)
                    .FirstOrDefaultAsync(d => d.DeliveryId == id);
                if (delivery == null) return NotFound();
                if (!DeliveryWorkflow.CanEdit(delivery))
                {
                    ErrorMessage = "Thủ kho đã lập phiếu xuất kho hoặc yêu cầu đã đóng nên không thể chỉnh sửa.";
                    return RedirectToPage("/Deliveries/Detail", new { id });
                }

                await LoadOrderAsync(delivery.OrderId);
                DeliveryId = delivery.DeliveryId;
                ReturnReason = delivery.ReturnReason;
                Input = new DeliveryInput
                {
                    DriverName = delivery.DriverName,
                    DriverPhone = delivery.DriverPhone,
                    LicensePlate = delivery.LicensePlate,
                    PickupDate = delivery.PickupDate,
                    Note = delivery.Note
                };
                return Page();
            }

            if (!orderId.HasValue) return RedirectToPage("/Deliveries/Index");

            var error = await CheckCanCreateAsync(orderId.Value);
            if (error != null)
            {
                ErrorMessage = error;
                return RedirectToPage("/Orders/Detail", new { id = orderId.Value });
            }

            await LoadOrderAsync(orderId.Value);
            var now = DateTime.Now;
            Input.PickupDate = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id, int? orderId)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            Delivery? delivery = null;
            if (id.HasValue)
            {
                delivery = await _context.Deliveries.Include(d => d.ExportSlip).FirstOrDefaultAsync(d => d.DeliveryId == id);
                if (delivery == null) return NotFound();
                if (!DeliveryWorkflow.CanEdit(delivery))
                {
                    ErrorMessage = "Thủ kho đã lập phiếu xuất kho hoặc yêu cầu đã đóng nên không thể chỉnh sửa.";
                    return RedirectToPage("/Deliveries/Detail", new { id });
                }
                orderId = delivery.OrderId;
                DeliveryId = delivery.DeliveryId;
                ReturnReason = delivery.ReturnReason;
            }
            else
            {
                if (!orderId.HasValue) return RedirectToPage("/Deliveries/Index");
                var error = await CheckCanCreateAsync(orderId.Value);
                if (error != null)
                {
                    ErrorMessage = error;
                    return RedirectToPage("/Orders/Detail", new { id = orderId.Value });
                }
            }

            await LoadOrderAsync(orderId!.Value);
            ValidateInput();
            if (!ModelState.IsValid) return Page();

            var plate = Regex.Replace(Input.LicensePlate!.Trim(), @"\s+", " ").ToUpperInvariant();
            var now = DateTime.Now;
            var creating = delivery == null;
            if (creating)
            {
                delivery = new Delivery
                {
                    OrderId = orderId.Value,
                    DeliveryStatus = StatusConstants.Prepared,
                    CreatedBy = CurrentUserId,
                    CreatedAt = now
                };
                _context.Deliveries.Add(delivery);
            }

            delivery!.DriverName = Input.DriverName!.Trim();
            delivery.DriverPhone = string.IsNullOrWhiteSpace(Input.DriverPhone) ? null : Input.DriverPhone.Trim();
            delivery.LicensePlate = plate;
            delivery.PickupDate = Input.PickupDate!.Value;
            delivery.Note = string.IsNullOrWhiteSpace(Input.Note) ? null : Input.Note.Trim();
            delivery.UpdatedAt = now;
            // Sau khi sửa theo yêu cầu của thủ kho, gửi lại để kiểm tra.
            delivery.DeliveryStatus = StatusConstants.Prepared;
            delivery.ReturnReason = null;

            await SalesFlow.NotifyRoleAsync(_context, RoleConstants.WarehouseStaff,
                creating ? "Yêu cầu xuất kho mới" : "Yêu cầu xuất kho đã được cập nhật",
                $"{Order.OrderCode}: tài xế {delivery.DriverName}, biển số {delivery.LicensePlate}, lấy hàng {delivery.PickupDate:dd/MM/yyyy HH:mm}. Vui lòng kiểm tra thông tin.",
                "/Deliveries/Index");

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DataValidationException ex)
            {
                ModelState.AddModelError(string.Empty, string.Join(" ", ex.Errors));
                return Page();
            }

            SuccessMessage = creating
                ? $"Đã tạo yêu cầu xuất kho cho {Order.OrderCode} và gửi thủ kho."
                : $"Đã cập nhật yêu cầu xuất kho của {Order.OrderCode} và gửi lại thủ kho.";
            return RedirectToPage("/Deliveries/Detail", new { id = delivery.DeliveryId });
        }

        private void ValidateInput()
        {
            if (!string.IsNullOrWhiteSpace(Input.DriverPhone) && !PhonePattern.IsMatch(Input.DriverPhone.Trim()))
                ModelState.AddModelError("Input.DriverPhone", "Số điện thoại tài xế không hợp lệ.");
            if (!string.IsNullOrWhiteSpace(Input.LicensePlate) && !PlatePattern.IsMatch(Input.LicensePlate.Trim()))
                ModelState.AddModelError("Input.LicensePlate", "Biển số xe không hợp lệ (4–20 ký tự gồm chữ, số, dấu - hoặc .).");
            if (Input.PickupDate.HasValue && Input.PickupDate.Value.Date < DateTime.Today)
                ModelState.AddModelError("Input.PickupDate", "Ngày lấy hàng không được trước ngày hôm nay.");
        }

        private async Task<string?> CheckCanCreateAsync(int orderId)
        {
            var order = await _context.Orders.AsNoTracking()
                .Include(o => o.Deliveries)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return "Yêu cầu bán không tồn tại.";
            if (order.Status != StatusConstants.Approved)
                return $"Yêu cầu {order.OrderCode} chưa được Owner duyệt hoặc đã đóng nên không thể tạo yêu cầu xuất kho.";
            if (OrderWorkflow.ActiveDelivery(order) != null)
                return $"Yêu cầu {order.OrderCode} đã có yêu cầu xuất kho. Hãy cập nhật yêu cầu hiện có.";
            return null;
        }

        private async Task LoadOrderAsync(int orderId)
        {
            Order = await _context.Orders.AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.OrderDetails).ThenInclude(d => d.Batch)
                .AsSplitQuery()
                .FirstAsync(o => o.OrderId == orderId);
        }
    }
}
