using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using BATT_PPMS.Pages.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Deliveries
{
    /// <summary>
    /// Screen: Chi tiết yêu cầu xuất kho. Thủ kho đối chiếu thông tin khi người mua đến:
    /// sai thì trả lại Manager sửa, đúng thì lập phiếu xuất kho.
    /// </summary>
    public class DetailModel : BasePageModel
    {
        private const long MaxImageBytes = 10 * 1024 * 1024;
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly ProductionManagementDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DetailModel(ProductionManagementDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public Delivery Delivery { get; private set; } = null!;
        public Order Order => Delivery.Order;

        public bool CanCheck => IsWarehouseStaff && Delivery.DeliveryStatus == StatusConstants.Prepared
                                && Delivery.ExportSlip == null && Order.Status == StatusConstants.Approved;
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
                .Include(d => d.ExportSlip)
                .Include(d => d.Order).ThenInclude(o => o.Customer)
                .Include(d => d.Order).ThenInclude(o => o.OrderDetails).ThenInclude(x => x.Batch)
                .AsSplitQuery()
                .FirstOrDefaultAsync(d => d.DeliveryId == id);
            if (delivery == null) return NotFound();

            Delivery = delivery;
            return Page();
        }

        /// <summary>Thông tin đúng: lập phiếu xuất kho (các dòng lấy từ lô đã phân bổ FIFO của yêu cầu bán).</summary>
        public async Task<IActionResult> OnPostCreateSlipAsync(int id, IFormFile? slipImage)
        {
            var check = RequireRole(RoleConstants.WarehouseStaff);
            if (check != null) return check;

            var delivery = await _context.Deliveries
                .Include(d => d.ExportSlip)
                .Include(d => d.Order).ThenInclude(o => o.OrderDetails).ThenInclude(x => x.Batch)
                .AsSplitQuery()
                .FirstOrDefaultAsync(d => d.DeliveryId == id);
            if (delivery == null) return NotFound();

            var order = delivery.Order;
            if (delivery.DeliveryStatus != StatusConstants.Prepared || delivery.ExportSlip != null || order.Status != StatusConstants.Approved)
                return Fail(id, "Yêu cầu xuất kho này không còn ở trạng thái chờ kiểm tra.");
            if (order.OrderDetails.Count == 0)
                return Fail(id, "Yêu cầu bán chưa có lô thành phẩm được phân bổ.");

            foreach (var line in order.OrderDetails)
            {
                if (line.Batch.Status != "In Stock" || line.Batch.Quantity < line.Quantity)
                    return Fail(id, $"Lô {line.Batch.BagNumber} không còn đủ hàng ({SalesFlow.Bags(line.Batch.Quantity)} bao) để xuất {SalesFlow.Bags(line.Quantity)} bao.");
            }

            var imageError = ValidateImage(slipImage);
            if (imageError != null) return Fail(id, imageError);

            var codes = await _context.ExportSlips.AsNoTracking().Select(s => s.SlipCode).ToListAsync();
            var slip = new ExportSlip
            {
                SlipCode = SalesFlow.NextCode(codes, "PXK"),
                Delivery = delivery,
                Order = order,
                Status = ExportSlipStatus.Processing,
                CreatedBy = CurrentUserId,
                CreatedAt = DateTime.Now
            };
            _context.ExportSlips.Add(slip);
            foreach (var line in order.OrderDetails.OrderBy(x => x.Batch.ProductionDate).ThenBy(x => x.BatchId))
            {
                slip.Lines.Add(new ExportSlipLine
                {
                    BatchId = line.BatchId,
                    Grade = order.Grade,
                    Bags = (int)Math.Round(line.Quantity / SalesFlow.KgPerBag),
                    Quantity = line.Quantity
                });
            }

            await SalesFlow.NotifyUserOrRoleAsync(_context, delivery.CreatedBy ?? order.CreatedBy, RoleConstants.Manager,
                "Thủ kho đã xác nhận yêu cầu xuất kho",
                $"{order.OrderCode}: thông tin hợp lệ, phiếu xuất kho {slip.SlipCode} đang được xử lý.",
                $"/Deliveries/Detail?id={delivery.DeliveryId}");

            if (slipImage is { Length: > 0 })
                slip.ImagePath = await SaveImageAsync(slipImage);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                DeleteImage(slip.ImagePath);
                return Fail(id, "Không thể tạo phiếu xuất kho (có thể phiếu đã được tạo). Vui lòng tải lại trang.");
            }
            catch (DataValidationException ex)
            {
                DeleteImage(slip.ImagePath);
                return Fail(id, string.Join(" ", ex.Errors));
            }

            SuccessMessage = $"Đã tạo phiếu xuất kho {slip.SlipCode}.";
            return RedirectToPage("/ExportSlips/Detail", new { id = slip.SlipId });
        }

        /// <summary>Thông tin sai: gửi lại Manager chỉnh sửa, bắt buộc ghi lý do.</summary>
        public async Task<IActionResult> OnPostReturnAsync(int id, string? reason)
        {
            var check = RequireRole(RoleConstants.WarehouseStaff);
            if (check != null) return check;

            var delivery = await _context.Deliveries
                .Include(d => d.ExportSlip)
                .Include(d => d.Order)
                .FirstOrDefaultAsync(d => d.DeliveryId == id);
            if (delivery == null) return NotFound();

            if (delivery.DeliveryStatus != StatusConstants.Prepared || delivery.ExportSlip != null)
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

        private static string? ValidateImage(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null; // ảnh là tùy chọn
            if (file.Length > MaxImageBytes) return "Ảnh phiếu xuất kho không được vượt quá 10 MB.";
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext) || !(file.ContentType?.StartsWith("image/") ?? false))
                return "Ảnh phiếu xuất kho phải là tệp JPG, PNG hoặc WEBP.";
            return null;
        }

        private async Task<string> SaveImageAsync(IFormFile file)
        {
            var dir = Path.Combine(_env.WebRootPath, "uploads", "export-slips");
            Directory.CreateDirectory(dir);
            var name = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
            await using (var stream = System.IO.File.Create(Path.Combine(dir, name)))
                await file.CopyToAsync(stream);
            return $"/uploads/export-slips/{name}";
        }

        private void DeleteImage(string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return;
            var full = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
        }

        private IActionResult Fail(int id, string message)
        {
            ErrorMessage = message;
            return RedirectToPage(new { id });
        }
    }
}
