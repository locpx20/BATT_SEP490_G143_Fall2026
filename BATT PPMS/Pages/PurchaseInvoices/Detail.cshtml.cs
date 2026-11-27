using System.Globalization;
using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.PurchaseInvoices
{
    /// <summary>
    /// Bảng 1: Phiếu mua hàng (Purchase Order - PM-26xxx).
    /// Một Phiếu mua hàng sau khi được Manager duyệt có thể tạo nhiều Yêu cầu nhập kho (Inbound Request - IR-26xxx).
    /// </summary>
    public class PurchaseReceiptViewModel
    {
        public int ReceiptId { get; set; }
        public string Code { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total => Quantity * Price;
        public decimal RequestedInboundQuantity { get; set; }
        public decimal ReceivedInboundQuantity { get; set; }
        public decimal RemainingToRequestQuantity => Math.Max(0m, Quantity - RequestedInboundQuantity);
        public int InboundRequestCount { get; set; }
        public string InboundCodesSummary { get; set; } = string.Empty;
        public string RawStatus { get; set; } = "PendingOrderApproval";
        public string StatusText { get; set; } = string.Empty;
        public string StatusType { get; set; } = "pending"; // pending | warehouse | completed | received | rejected
        public string? RejectionReason { get; set; }
        public string ScalePhoto { get; set; } = string.Empty;
        public string? InvoicePhoto { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public bool CanCreateMoreInbound =>
            (RawStatus == "OrderApproved" || RawStatus == "WaitWarehouse") &&
            RemainingToRequestQuantity > 0;
    }

    /// <summary>
    /// Bảng 2: Yêu cầu nhập kho (Inbound Request - IR-26xxx) tách riêng khỏi Phiếu mua hàng (PM-26xxx).
    /// </summary>
    public class InboundRequestItemViewModel
    {
        public int InboundRequestId { get; set; }
        public string RequestCode { get; set; } = string.Empty;
        public int ReceiptId { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal DeliveryQuantity { get; set; }
        public decimal? ImportedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total => (ImportedQuantity ?? DeliveryQuantity) * UnitPrice;
        public string LicensePlate { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string DeliveryTime { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string RawStatus { get; set; } = "WaitWarehouse";
        public string StatusText { get; set; } = string.Empty;
        public string StatusType { get; set; } = "warehouse"; // pending | warehouse | received | rejected
        public string? RejectionReason { get; set; }
        public string ScalePhoto { get; set; } = string.Empty;
        public string? InvoicePhoto { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Thông tin Phiếu nhập kho nguyên liệu liên kết (PNK-26xxx)
        public int? ImportId { get; set; }
        public string? ImportCode { get; set; }
        public string? ImportStatus { get; set; }
        public string? ImportedByName { get; set; }
        public DateTime? ImportDate { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }

    public class TimelineEntry
    {
        public DateTime At { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class AssignedInvoiceOption
    {
        public int InvoiceId { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    public class DetailModel : BasePageModel
    {
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".jfif", ".heic" };
        private const long MaxImageBytes = 20 * 1024 * 1024;

        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DetailModel(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        public int InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string StatusType { get; set; } = "procuring";
        public decimal PlannedQuantity { get; set; }
        public decimal CeilingPrice { get; set; }
        public decimal WeighedTotal { get; set; }
        public decimal InboundRequestedTotal { get; set; }
        public decimal InboundReceivedTotal { get; set; }
        public decimal RemainingQuantity => Math.Max(0m, PlannedQuantity - WeighedTotal);
        public string AssignedName { get; set; } = string.Empty;
        public List<StaffAssignmentNote> StaffAssignments { get; set; } = new();
        public string CreatorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsExpired { get; set; }

        public List<PurchaseReceiptViewModel> Receipts { get; set; } = new();
        public List<InboundRequestItemViewModel> InboundRequests { get; set; } = new();
        public List<TimelineEntry> Timeline { get; set; } = new();
        public List<PurchaseSupplierOption> Suppliers { get; set; } = new();
        public List<AssignedInvoiceOption> InvoiceOptions { get; set; } = new();

        public bool CanCreateReceipt { get; set; }
        public bool CanManagerReviewOrder => IsManager;
        public bool IsOwnerView => IsOwner;
        public bool CanCreateInboundRequest => IsPurchasingStaff;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager, RoleConstants.PurchasingStaff);
            if (check != null) return check;

            await IndexModel.ExpirePastDayInvoicesAsync(_context);
            return await LoadAsync(Id) ? Page() : NotFound();
        }

        private static bool TryParsePositiveDecimal(string? raw, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var cleaned = raw.Trim().Replace(" ", "");
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out value) && value > 0)
                return true;
            if (decimal.TryParse(cleaned, NumberStyles.Any, new CultureInfo("vi-VN"), out value) && value > 0)
                return true;
            return false;
        }

        // BƯỚC 2 TRÊN SWIMLANE: Chỉ Purchasing Staff -> Create Purchase Order (PM-26xxx) (hoặc sửa lại sau khi bị từ chối)
        public async Task<IActionResult> OnPostCreateReceiptAsync(
            int invoiceId,
            int? editReceiptId,
            int supplierId,
            string? weighedQuantity,
            string? actualPrice,
            IFormFile? scalePhoto,
            IFormFile? invoicePhoto,
            bool returnToIndex = false)
        {
            var check = RequireRole(RoleConstants.PurchasingStaff);
            if (check != null) return check;

            var targetId = invoiceId > 0 ? invoiceId : Id;
            IActionResult Back() => returnToIndex
                ? RedirectToPage("/PurchaseInvoices/Index")
                : RedirectToPage(new { id = targetId });

            await IndexModel.ExpirePastDayInvoicesAsync(_context);

            var invoice = await _context.PurchaseInvoices
                .AsNoTracking()
                .Include(p => p.PurchaseInvoiceDetails)
                .FirstOrDefaultAsync(p => p.InvoiceId == targetId);
            if (invoice == null) return NotFound();

            if (IndexModel.IsInvoiceExpiredByDate(invoice.PurchaseDate) ||
                string.Equals(invoice.Status, "Expired", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = $"Yêu cầu thu mua {IndexModel.FormatInvoiceBadgeCode(invoice.InvoiceCode, invoice.InvoiceId)} đã hết hạn (chỉ có hiệu lực đến 23:59 ngày {invoice.PurchaseDate:dd/MM/yyyy}). Không thể tạo thêm phiếu mua hàng.";
                return Back();
            }

            if (IsPurchasingStaff && !NoteTags.IsStaffAssigned(invoice.Note, invoice.AssignedTo, invoice.CreatedBy, CurrentUserId))
            {
                ErrorMessage = "Yêu cầu thu mua này không được giao cho bạn.";
                return Back();
            }

            if (!TryParsePositiveDecimal(weighedQuantity, out var parsedWeight) ||
                parsedWeight > PurchasePricing.MaxOrderQuantity)
            {
                ErrorMessage = $"Khối lượng đặt mua phải lớn hơn 0 và không vượt quá {PurchasePricing.MaxOrderQuantity:N0} kg.";
                return Back();
            }

            var plannedQty = invoice.PurchaseInvoiceDetails.Sum(d => d.Quantity);
            var existingTotalWeight = await _context.PurchaseReceipts
                .AsNoTracking()
                .Where(r => r.InvoiceId == invoice.InvoiceId
                            && r.Status != "Rejected"
                            && r.Status != "OrderRejected"
                            && (!editReceiptId.HasValue || r.ReceiptId != editReceiptId.Value))
                .SumAsync(r => r.WeighedQuantity);

            if (plannedQty > 0 && existingTotalWeight + parsedWeight > plannedQty)
            {
                var remainingQty = Math.Max(0m, plannedQty - existingTotalWeight);
                ErrorMessage = $"Tổng khối lượng các phiếu mua hàng ({existingTotalWeight + parsedWeight:N0} kg) vượt quá khối lượng cần mua của yêu cầu thu mua ({plannedQty:N0} kg). Khối lượng còn lại có thể nhập tối đa là {remainingQty:N0} kg.";
                return Back();
            }

            var ceiling = invoice.PurchaseInvoiceDetails.Select(d => d.UnitPrice).DefaultIfEmpty(0).Max();
            if (!TryParsePositiveDecimal(actualPrice, out var parsedPrice) ||
                parsedPrice > PurchasePricing.MaxUnitPrice)
            {
                ErrorMessage = "Giá mua thực tế không hợp lệ.";
                return Back();
            }
            if (ceiling > 0 && parsedPrice > ceiling)
            {
                ErrorMessage = $"Giá mua thực tế ({parsedPrice:N0}) không được vượt giá trần của yêu cầu thu mua ({ceiling:N0} VNĐ/kg).";
                return Back();
            }

            var supplier = await _context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.SupplierId == supplierId);
            if (supplier == null || supplier.Status != StatusConstants.Active)
            {
                ErrorMessage = "Nhà cung cấp không tồn tại hoặc đã ngừng hoạt động.";
                return Back();
            }

            // Nếu là sửa lại đơn mua bị từ chối (Rejection Reason -> Create Purchase Order)
            if (editReceiptId.HasValue && editReceiptId.Value > 0)
            {
                var existingReceipt = await _context.PurchaseReceipts
                    .FirstOrDefaultAsync(r => r.ReceiptId == editReceiptId.Value && r.InvoiceId == invoice.InvoiceId);
                if (existingReceipt == null)
                {
                    ErrorMessage = "Không tìm thấy phiếu mua hàng cần chỉnh sửa.";
                    return Back();
                }

                var scaleErr = ValidateImage(scalePhoto, required: false, "Ảnh minh chứng / hàng hóa");
                if (scaleErr != null) { ErrorMessage = scaleErr; return Back(); }
                var invErr = ValidateImage(invoicePhoto, required: false, "Hóa đơn");
                if (invErr != null) { ErrorMessage = invErr; return Back(); }

                existingReceipt.SupplierId = supplier.SupplierId;
                existingReceipt.WeighedQuantity = parsedWeight;
                existingReceipt.ActualPrice = parsedPrice;
                if (scalePhoto is { Length: > 0 })
                    existingReceipt.ScalePhotoPath = await SavePhotoAsync(scalePhoto);
                if (invoicePhoto is { Length: > 0 })
                    existingReceipt.InvoicePhotoPath = await SavePhotoAsync(invoicePhoto);

                existingReceipt.Status = "PendingOrderApproval";
                existingReceipt.RejectionReason = null;

                await NotifyManagersAsync(
                    $"Đơn mua {existingReceipt.ReceiptCode} đã chỉnh sửa & gửi lại",
                    $"{CurrentFullName} đã chỉnh sửa và gửi lại đơn mua {existingReceipt.ReceiptCode} ({parsedWeight:N0} kg × {parsedPrice:N0} VNĐ/kg), chờ Manager phê duyệt.",
                    $"/PurchaseInvoices/Detail?id={invoice.InvoiceId}");

                await _context.SaveChangesAsync();
                await IndexModel.ExpirePastDayInvoicesAsync(_context);
                SuccessMessage = $"Đã cập nhật và gửi lại đơn mua {existingReceipt.ReceiptCode} cho Manager phê duyệt.";
                return Back();
            }

            var scaleError = ValidateImage(scalePhoto, required: true, "Ảnh minh chứng / hàng hóa");
            if (scaleError != null) { ErrorMessage = scaleError; return Back(); }
            var invoiceImgError = ValidateImage(invoicePhoto, required: false, "Hóa đơn");
            if (invoiceImgError != null) { ErrorMessage = invoiceImgError; return Back(); }

            var prefix = $"PM-{DateTime.Now.Year % 100:D2}";
            var existing = await _context.PurchaseReceipts
                .AsNoTracking()
                .Where(r => r.ReceiptCode.StartsWith(prefix))
                .Select(r => r.ReceiptCode)
                .ToListAsync();
            var nextSeq = existing
                .Select(c => int.TryParse(c.Substring(prefix.Length), out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;
            var code = $"{prefix}{nextSeq:D3}";

            var scalePath = await SavePhotoAsync(scalePhoto!);
            var invoicePath = invoicePhoto is { Length: > 0 } ? await SavePhotoAsync(invoicePhoto) : null;

            _context.PurchaseReceipts.Add(new PurchaseReceipt
            {
                ReceiptCode = code,
                InvoiceId = invoice.InvoiceId,
                SupplierId = supplier.SupplierId,
                WeighedQuantity = parsedWeight,
                ActualPrice = parsedPrice,
                ScalePhotoPath = scalePath,
                InvoicePhotoPath = invoicePath,
                Status = "PendingOrderApproval",
                CreatedBy = CurrentUserId,
                CreatedAt = DateTime.Now
            });

            try
            {
                await NotifyManagersAsync(
                    $"Phiếu mua hàng {code} chờ duyệt",
                    $"{CurrentFullName} vừa tạo phiếu mua hàng {code} ({parsedWeight:N0} kg × {parsedPrice:N0} VNĐ/kg) trên yêu cầu thu mua {IndexModel.FormatInvoiceBadgeCode(invoice.InvoiceCode, invoice.InvoiceId)}, chờ Manager phê duyệt.",
                    $"/PurchaseInvoices/Detail?id={invoice.InvoiceId}");

                await _context.SaveChangesAsync();
                await IndexModel.ExpirePastDayInvoicesAsync(_context);
            }
            catch (DataValidationException ex)
            {
                ErrorMessage = ex.Message;
                return Back();
            }
            catch (DbUpdateException ex)
            {
                ErrorMessage = $"Không thể tạo phiếu mua hàng: {ex.InnerException?.Message ?? ex.Message}";
                return Back();
            }

            SuccessMessage = $"Đã tạo phiếu mua hàng {code} ({parsedWeight:N0} kg × {parsedPrice:N0} VNĐ/kg) và gửi cho Manager phê duyệt.";
            return Back();
        }

        // BƯỚC 3A TRÊN SWIMLANE: Chỉ Manager -> Approved? -> Yes
        public async Task<IActionResult> OnPostApproveOrderAsync(int receiptId)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var receipt = await _context.PurchaseReceipts.FirstOrDefaultAsync(r => r.ReceiptId == receiptId);
            if (receipt == null) return NotFound();

            receipt.Status = "OrderApproved";
            receipt.RejectionReason = null;

            if (receipt.CreatedBy.HasValue)
            {
                Notifier.Add(
                    _context,
                    receipt.CreatedBy.Value,
                    $"Phiếu mua hàng {receipt.ReceiptCode} đã được duyệt",
                    $"Manager ({CurrentFullName}) đã phê duyệt phiếu mua hàng {receipt.ReceiptCode}. Vui lòng bấm '+ Tạo yêu cầu nhập kho' để lập các đợt giao hàng (IR-26xxx) gửi sang Kho.",
                    $"/PurchaseInvoices/Detail?id={receipt.InvoiceId}");
            }

            await _context.SaveChangesAsync();
            SuccessMessage = $"Đã phê duyệt phiếu mua hàng {receipt.ReceiptCode}. Nhân viên thu mua có thể tạo một hoặc nhiều Yêu cầu nhập kho (IR-26xxx) từ phiếu mua hàng này.";
            return RedirectToPage(new { id = receipt.InvoiceId });
        }

        // BƯỚC 3B TRÊN SWIMLANE: Chỉ Manager -> Approved? -> No -> Rejection Reason -> quay lại Create Purchase Order
        public async Task<IActionResult> OnPostRejectOrderAsync(int receiptId, string? rejectionReason)
        {
            var check = RequireRole(RoleConstants.Manager);
            if (check != null) return check;

            var receipt = await _context.PurchaseReceipts.FirstOrDefaultAsync(r => r.ReceiptId == receiptId);
            if (receipt == null) return NotFound();

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                ErrorMessage = "Vui lòng nhập lý do từ chối (Rejection Reason) cho phiếu mua hàng.";
                return RedirectToPage(new { id = receipt.InvoiceId });
            }

            var reason = rejectionReason.Trim();
            receipt.Status = "OrderRejected";
            receipt.RejectionReason = reason;

            if (receipt.CreatedBy.HasValue)
            {
                Notifier.Add(
                    _context,
                    receipt.CreatedBy.Value,
                    $"Phiếu mua hàng {receipt.ReceiptCode} bị từ chối",
                    $"Manager ({CurrentFullName}) đã từ chối phiếu mua hàng {receipt.ReceiptCode}. Lý do: {reason}. Vui lòng bấm 'Sửa & gửi lại'.",
                    $"/PurchaseInvoices/Detail?id={receipt.InvoiceId}");
            }

            await _context.SaveChangesAsync();
            SuccessMessage = $"Đã từ chối phiếu mua hàng {receipt.ReceiptCode} kèm lý do và gửi trả về cho Nhân viên thu mua chỉnh sửa.";
            return RedirectToPage(new { id = receipt.InvoiceId });
        }

        // BƯỚC 4 TRÊN SWIMLANE: Purchasing Staff -> Create Inbound Request (IR-26xxx) từ 1 Purchase Order (PM-26xxx)
        // 1 Purchase Order (PM-26xxx) có thể tạo nhiều Inbound Request (IR-26xxx) tương ứng với nhiều đợt / chuyến xe giao hàng.
        public async Task<IActionResult> OnPostCreateInboundRequestAsync(
            int receiptId,
            string? deliveryQuantity,
            string? licensePlate,
            string? driverName,
            DateTime? deliveryTime,
            string? deliveryNote)
        {
            var check = RequireRole(RoleConstants.PurchasingStaff);
            if (check != null) return check;

            var receipt = await _context.PurchaseReceipts
                .Include(r => r.Supplier)
                .Include(r => r.Invoice)
                .Include(r => r.InboundRequests)
                .FirstOrDefaultAsync(r => r.ReceiptId == receiptId);
            if (receipt == null) return NotFound();

            if (receipt.Status != "OrderApproved" && receipt.Status != "WaitWarehouse")
            {
                ErrorMessage = $"Phiếu mua hàng {receipt.ReceiptCode} cần được Manager phê duyệt trước khi tạo yêu cầu nhập kho.";
                return RedirectToPage(new { id = receipt.InvoiceId });
            }

            var existingRequestedQty = receipt.InboundRequests.Sum(ir => ir.DeliveryQuantity);
            var remainingOrderQty = Math.Max(0m, receipt.WeighedQuantity - existingRequestedQty);
            if (remainingOrderQty <= 0)
            {
                ErrorMessage = $"Phiếu mua hàng {receipt.ReceiptCode} đã tạo đủ 100% khối lượng sang Yêu cầu nhập kho ({receipt.WeighedQuantity:N0} kg).";
                return RedirectToPage(new { id = receipt.InvoiceId });
            }

            decimal parsedDeliveryQty = remainingOrderQty;
            if (!string.IsNullOrWhiteSpace(deliveryQuantity))
            {
                if (!TryParsePositiveDecimal(deliveryQuantity, out parsedDeliveryQty))
                {
                    ErrorMessage = "Khối lượng giao hàng của đợt này phải lớn hơn 0.";
                    return RedirectToPage(new { id = receipt.InvoiceId });
                }
            }

            if (parsedDeliveryQty > remainingOrderQty)
            {
                ErrorMessage = $"Khối lượng yêu cầu nhập kho đợt này ({parsedDeliveryQty:N0} kg) vượt quá khối lượng còn lại của phiếu mua hàng {receipt.ReceiptCode} ({remainingOrderQty:N0} / {receipt.WeighedQuantity:N0} kg).";
                return RedirectToPage(new { id = receipt.InvoiceId });
            }

            if (string.IsNullOrWhiteSpace(licensePlate) ||
                string.IsNullOrWhiteSpace(driverName) ||
                !deliveryTime.HasValue)
            {
                ErrorMessage = "Vui lòng điền đầy đủ Biển số xe, Tên lái xe và Thời gian giao hàng khi tạo yêu cầu nhập kho.";
                return RedirectToPage(new { id = receipt.InvoiceId });
            }

            var cleanPlate = licensePlate.Trim().ToUpperInvariant();
            var cleanDriver = driverName.Trim();
            var deliveryTimeText = deliveryTime.Value.ToString("HH:mm dd/MM/yyyy");

            // Sinh mã Yêu cầu nhập kho riêng biệt: IR-26xxx
            var prefix = $"IR-{DateTime.Now.Year % 100:D2}";
            var existingCodes = await _context.InboundRequests
                .AsNoTracking()
                .Where(ir => ir.RequestCode.StartsWith(prefix))
                .Select(ir => ir.RequestCode)
                .ToListAsync();
            var nextSeq = existingCodes
                .Select(c => int.TryParse(c.Substring(prefix.Length), out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;
            var requestCode = $"{prefix}{nextSeq:D3}";

            var inboundReq = new InboundRequest
            {
                RequestCode = requestCode,
                ReceiptId = receipt.ReceiptId,
                InvoiceId = receipt.InvoiceId,
                SupplierId = receipt.SupplierId,
                DeliveryQuantity = parsedDeliveryQty,
                UnitPrice = receipt.ActualPrice,
                LicensePlate = cleanPlate,
                DriverName = cleanDriver,
                DeliveryTime = deliveryTime.Value,
                ScalePhotoPath = receipt.ScalePhotoPath,
                InvoicePhotoPath = receipt.InvoicePhotoPath,
                Status = "WaitWarehouse",
                Note = string.IsNullOrWhiteSpace(deliveryNote) ? null : deliveryNote.Trim(),
                CreatedBy = CurrentUserId,
                CreatedAt = DateTime.Now
            };

            _context.InboundRequests.Add(inboundReq);

            // Gửi Inbound Request Notification cho toàn bộ Thủ kho (Warehouse Staff)
            var warehouseUserIds = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Status == StatusConstants.Active && u.Role.RoleName == RoleConstants.WarehouseStaff)
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var whUserId in warehouseUserIds)
            {
                Notifier.Add(
                    _context,
                    whUserId,
                    $"Yêu cầu nhập kho mới {requestCode} (từ {receipt.ReceiptCode})",
                    $"Nhân viên thu mua ({CurrentFullName}) đã tạo Yêu cầu nhập kho {requestCode} thuộc phiếu mua hàng {receipt.ReceiptCode} — {parsedDeliveryQty:N0} kg từ {receipt.Supplier?.SupplierName} (Biển số xe: {cleanPlate} · Lái xe: {cleanDriver} · Giao lúc: {deliveryTimeText}). Vui lòng kiểm tra đối chiếu và lập Phiếu nhập kho nguyên liệu.",
                    "/Inventory/Index?tab=raw");
            }

            await _context.SaveChangesAsync();
            var afterRemaining = Math.Max(0m, remainingOrderQty - parsedDeliveryQty);
            var remainHint = afterRemaining > 0
                ? $" Khối lượng còn lại của {receipt.ReceiptCode} có thể tạo thêm IR tiếp theo: {afterRemaining:N0} kg."
                : $" Đã tạo đủ 100% khối lượng của phiếu mua hàng {receipt.ReceiptCode}.";
            SuccessMessage = $"Đã tạo Yêu cầu nhập kho {requestCode} ({parsedDeliveryQty:N0} kg — Xe: {cleanPlate} · Lái xe: {cleanDriver} · Giao lúc: {deliveryTimeText}) từ phiếu mua hàng {receipt.ReceiptCode} và gửi tới Thủ kho.{remainHint}";
            return RedirectToPage(new { id = receipt.InvoiceId });
        }

        private async Task NotifyManagersAsync(string title, string message, string url)
        {
            var managerIds = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Status == StatusConstants.Active &&
                            u.Role.RoleName == RoleConstants.Manager)
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var mgrId in managerIds)
            {
                Notifier.Add(_context, mgrId, title, message, url);
            }
        }

        private string? ValidateImage(IFormFile? file, bool required, string label)
        {
            if (file == null || file.Length == 0)
                return required ? $"Vui lòng chọn {label.ToLowerInvariant()}." : null;
            if (file.Length > MaxImageBytes)
                return $"{label} không được vượt quá 20 MB.";
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext) && !(file.ContentType?.StartsWith("image/") ?? false))
                return $"{label} phải là tệp hình ảnh hợp lệ.";
            return null;
        }

        private async Task<string> SavePhotoAsync(IFormFile file)
        {
            var dir = Path.Combine(_env.WebRootPath, "uploads", "purchase");
            Directory.CreateDirectory(dir);
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
            var name = $"{Guid.NewGuid():N}{ext}";
            await using (var stream = System.IO.File.Create(Path.Combine(dir, name)))
                await file.CopyToAsync(stream);
            return $"/uploads/purchase/{name}";
        }

        private async Task<bool> LoadAsync(int id)
        {
            var invoice = await _context.PurchaseInvoices
                .AsNoTracking()
                .Include(p => p.CreatedByNavigation)
                .Include(p => p.AssignedToNavigation)
                .Include(p => p.PurchaseInvoiceDetails)
                .Include(p => p.PurchaseReceipts).ThenInclude(r => r.Supplier)
                .Include(p => p.PurchaseReceipts).ThenInclude(r => r.CreatedByNavigation)
                .Include(p => p.PurchaseReceipts).ThenInclude(r => r.InboundRequests)
                .Include(p => p.InboundRequests).ThenInclude(ir => ir.Supplier)
                .Include(p => p.InboundRequests).ThenInclude(ir => ir.CreatedByNavigation)
                .Include(p => p.InboundRequests).ThenInclude(ir => ir.Receipt)
                .FirstOrDefaultAsync(p => p.InvoiceId == id);
            if (invoice == null) return false;

            var isAssignedToMe = NoteTags.IsStaffAssigned(invoice.Note, invoice.AssignedTo, invoice.CreatedBy, CurrentUserId);
            if (IsPurchasingStaff && !isAssignedToMe) return false;

            var receiptIds = invoice.PurchaseReceipts.Select(r => r.ReceiptId).ToList();
            var irIds = invoice.InboundRequests.Select(ir => ir.InboundRequestId).ToList();

            var imports = await _context.RawMaterialImports
                .AsNoTracking()
                .Include(i => i.ImportedByNavigation)
                .Include(i => i.ApprovedByNavigation)
                .Where(i => (i.InboundRequestId.HasValue && irIds.Contains(i.InboundRequestId.Value)) ||
                            (i.ReceiptId.HasValue && receiptIds.Contains(i.ReceiptId.Value)))
                .OrderByDescending(i => i.ImportId)
                .ToListAsync();

            var importByIrId = imports
                .Where(i => i.InboundRequestId.HasValue)
                .GroupBy(i => i.InboundRequestId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var importByReceiptId = imports
                .Where(i => !i.InboundRequestId.HasValue && i.ReceiptId.HasValue)
                .GroupBy(i => i.ReceiptId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            InvoiceId = invoice.InvoiceId;
            InvoiceCode = IndexModel.FormatInvoiceBadgeCode(invoice.InvoiceCode, invoice.InvoiceId);
            PlannedQuantity = invoice.PurchaseInvoiceDetails.Sum(d => d.Quantity);
            CeilingPrice = invoice.PurchaseInvoiceDetails.Select(d => d.UnitPrice).DefaultIfEmpty(0).Max();

            var activeReceipts = invoice.PurchaseReceipts
                .Where(r => r.Status != "Rejected" && r.Status != "OrderRejected")
                .ToList();
            var weighedQty = activeReceipts.Sum(r => r.WeighedQuantity);
            var receivedQty = invoice.InboundRequests.Any()
                ? invoice.InboundRequests.Where(ir => ir.Status == "Received").Sum(ir => ir.DeliveryQuantity)
                : activeReceipts.Where(r => r.Status == "Received").Sum(r => r.WeighedQuantity);

            var (statusText, statusType) = IndexModel.MapStatus(invoice.Status, invoice.PurchaseDate, weighedQty, PlannedQuantity, receivedQty);
            StatusText = statusText;
            StatusType = statusType;

            var fallbackName = invoice.AssignedToNavigation?.FullName ?? invoice.CreatedByNavigation?.FullName;
            StaffAssignments = NoteTags.GetAssignments(invoice.Note, invoice.AssignedTo ?? invoice.CreatedBy, fallbackName);
            AssignedName = StaffAssignments.Count > 0
                ? string.Join(", ", StaffAssignments.Select(a => a.FullName))
                : (fallbackName ?? "—");
            CreatorName = invoice.CreatedByNavigation?.FullName ?? "—";
            CreatedAt = invoice.PurchaseDate;
            ExpiresAt = invoice.PurchaseDate.Date.AddDays(1).AddSeconds(-1);
            IsExpired = IndexModel.IsInvoiceExpiredByDate(invoice.PurchaseDate) ||
                        string.Equals(invoice.Status, "Expired", StringComparison.OrdinalIgnoreCase);

            // BẢNG 1: Danh sách Phiếu mua hàng (Purchase Order - PM-26xxx)
            Receipts = invoice.PurchaseReceipts
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.ReceiptId)
                .Select(r =>
                {
                    var childIrs = r.InboundRequests
                        .OrderBy(ir => ir.InboundRequestId)
                        .ToList();
                    var reqIrQty = childIrs.Sum(ir => ir.DeliveryQuantity);
                    var recIrQty = childIrs.Where(ir => ir.Status == "Received").Sum(ir => ir.DeliveryQuantity);
                    var (poText, poType) = PurchaseOrderStatus(r.Status, r.WeighedQuantity, reqIrQty, recIrQty, childIrs.Count);

                    return new PurchaseReceiptViewModel
                    {
                        ReceiptId = r.ReceiptId,
                        Code = r.ReceiptCode,
                        SupplierId = r.SupplierId,
                        SupplierName = r.Supplier?.SupplierName ?? "—",
                        Quantity = r.WeighedQuantity,
                        Price = r.ActualPrice,
                        RequestedInboundQuantity = reqIrQty,
                        ReceivedInboundQuantity = recIrQty,
                        InboundRequestCount = childIrs.Count,
                        InboundCodesSummary = childIrs.Count > 0
                            ? string.Join(", ", childIrs.Select(ir => ir.RequestCode))
                            : "Chưa tạo IR",
                        RawStatus = r.Status ?? "PendingOrderApproval",
                        StatusText = poText,
                        StatusType = poType,
                        RejectionReason = r.RejectionReason,
                        ScalePhoto = r.ScalePhotoPath,
                        InvoicePhoto = r.InvoicePhotoPath,
                        CreatedByName = r.CreatedByNavigation?.FullName ?? "—",
                        CreatedAt = r.CreatedAt
                    };
                })
                .ToList();

            WeighedTotal = activeReceipts.Sum(r => r.WeighedQuantity);

            // BẢNG 2: Danh sách Yêu cầu nhập kho (Inbound Request - IR-26xxx)
            InboundRequests = invoice.InboundRequests
                .OrderByDescending(ir => ir.CreatedAt)
                .ThenByDescending(ir => ir.InboundRequestId)
                .Select(ir =>
                {
                    var (irText, irType) = InboundRequestStatus(ir.Status);
                    if (!importByIrId.TryGetValue(ir.InboundRequestId, out var imp))
                    {
                        importByReceiptId.TryGetValue(ir.ReceiptId, out imp);
                    }
                    var impCode = imp != null
                        ? (string.IsNullOrWhiteSpace(imp.ImportCode) ? $"PNK-26{imp.ImportId:D3}" : imp.ImportCode)
                        : null;

                    return new InboundRequestItemViewModel
                    {
                        InboundRequestId = ir.InboundRequestId,
                        RequestCode = ir.RequestCode,
                        ReceiptId = ir.ReceiptId,
                        ReceiptCode = ir.Receipt?.ReceiptCode ?? $"PM-26{ir.ReceiptId:D3}",
                        SupplierId = ir.SupplierId,
                        SupplierName = ir.Supplier?.SupplierName ?? "—",
                        DeliveryQuantity = ir.DeliveryQuantity,
                        ImportedQuantity = imp?.Quantity,
                        UnitPrice = ir.UnitPrice,
                        LicensePlate = ir.LicensePlate,
                        DriverName = ir.DriverName,
                        DeliveryTime = ir.DeliveryTime.ToString("HH:mm dd/MM/yyyy"),
                        Note = ir.Note,
                        RawStatus = ir.Status ?? "WaitWarehouse",
                        StatusText = irText,
                        StatusType = irType,
                        RejectionReason = ir.RejectionReason,
                        ScalePhoto = !string.IsNullOrWhiteSpace(ir.ScalePhotoPath)
                            ? ir.ScalePhotoPath!
                            : (ir.Receipt?.ScalePhotoPath ?? string.Empty),
                        InvoicePhoto = !string.IsNullOrWhiteSpace(ir.InvoicePhotoPath)
                            ? ir.InvoicePhotoPath
                            : ir.Receipt?.InvoicePhotoPath,
                        CreatedByName = ir.CreatedByNavigation?.FullName ?? "—",
                        CreatedAt = ir.CreatedAt,
                        ImportId = imp?.ImportId,
                        ImportCode = impCode,
                        ImportStatus = imp?.Status,
                        ImportedByName = imp?.ImportedByNavigation?.FullName,
                        ImportDate = imp?.ImportDate,
                        ApprovedByName = imp?.ApprovedByNavigation?.FullName,
                        ApprovedAt = imp?.ApprovedAt
                    };
                })
                .ToList();

            InboundRequestedTotal = InboundRequests.Sum(ir => ir.DeliveryQuantity);
            InboundReceivedTotal = InboundRequests
                .Where(ir => ir.RawStatus == "Received")
                .Sum(ir => ir.ImportedQuantity ?? ir.DeliveryQuantity);

            Timeline = Receipts
                .Select(r => new TimelineEntry { At = r.CreatedAt, Text = $"{r.CreatedByName} tạo phiếu mua hàng {r.Code} ({r.Quantity:N0} kg — {r.StatusText})" })
                .Concat(InboundRequests.Select(ir => new TimelineEntry
                {
                    At = ir.CreatedAt,
                    Text = $"{ir.CreatedByName} tạo yêu cầu nhập kho {ir.RequestCode} từ {ir.ReceiptCode} ({ir.DeliveryQuantity:N0} kg · Xe {ir.LicensePlate} — {ir.StatusText})"
                }))
                .Append(new TimelineEntry { At = invoice.PurchaseDate, Text = $"{CreatorName} tạo yêu cầu thu mua {InvoiceCode}" })
                .OrderByDescending(t => t.At)
                .ToList();

            CanCreateReceipt = !IsExpired &&
                               invoice.Status != "Rejected" &&
                               IsPurchasingStaff &&
                               isAssignedToMe;

            Suppliers = await _context.Suppliers.AsNoTracking()
                .Where(s => s.Status == StatusConstants.Active)
                .OrderBy(s => s.SupplierName)
                .Select(s => new PurchaseSupplierOption { SupplierId = s.SupplierId, Name = s.SupplierName })
                .ToListAsync();

            var today = DateTime.Today;
            var allTodayInvoices = await _context.PurchaseInvoices.AsNoTracking()
                .Where(p => p.Status != "Rejected" && p.Status != "Expired" && p.PurchaseDate >= today)
                .OrderByDescending(p => p.InvoiceId)
                .ToListAsync();
            if (IsPurchasingStaff)
            {
                allTodayInvoices = allTodayInvoices
                    .Where(p => NoteTags.IsStaffAssigned(p.Note, p.AssignedTo, p.CreatedBy, CurrentUserId))
                    .ToList();
            }

            InvoiceOptions = allTodayInvoices
                .Select(p => new AssignedInvoiceOption
                {
                    InvoiceId = p.InvoiceId,
                    Code = IndexModel.FormatInvoiceBadgeCode(p.InvoiceCode, p.InvoiceId)
                })
                .ToList();

            if (!IsExpired && InvoiceOptions.All(o => o.InvoiceId != InvoiceId))
            {
                InvoiceOptions.Insert(0, new AssignedInvoiceOption
                {
                    InvoiceId = InvoiceId,
                    Code = InvoiceCode
                });
            }

            return true;
        }

        public static (string Text, string Type) PurchaseOrderStatus(
            string? status,
            decimal orderQty,
            decimal requestedIrQty,
            decimal receivedIrQty,
            int irCount)
        {
            var s = status ?? "PendingOrderApproval";
            if (s == "PendingOrderApproval")
                return ("Chờ duyệt đơn", "pending");
            if (s == "OrderRejected" || s == "Rejected")
                return ("Từ chối đơn", "rejected");
            if (s == "Received" || (orderQty > 0 && receivedIrQty >= orderQty))
                return ("Đã nhập kho đủ", "received");

            if (irCount == 0)
                return ("Đã duyệt — Chờ tạo IR", "warehouse");
            if (orderQty > 0 && requestedIrQty < orderQty)
                return ($"Đang giao ({irCount} IR · {requestedIrQty:N0}/{orderQty:N0} kg)", "warehouse");
            return ($"Đã tạo đủ IR ({irCount} đợt)", "completed");
        }

        public static (string Text, string Type) InboundRequestStatus(string? status) => (status ?? "WaitWarehouse") switch
        {
            "WaitWarehouse" or "Pending" => ("Chờ nhập kho", "warehouse"),
            "WaitManagerApproval" => ("Chờ duyệt PNK", "pending"),
            "ImportRejected" => ("Từ chối PNK", "rejected"),
            "Received" => ("Đã nhập kho", "received"),
            _ => ("Chờ nhập kho", "warehouse")
        };

        public static (string Text, string Type) ReceiptStatus(string? status) => (status ?? "PendingOrderApproval") switch
        {
            "PendingOrderApproval" => ("Chờ duyệt đơn", "pending"),
            "OrderApproved" => ("Chờ gửi kho", "warehouse"),
            "OrderRejected" => ("Từ chối đơn", "rejected"),
            "WaitWarehouse" or "Pending" => ("Chờ nhập kho", "warehouse"),
            "WaitManagerApproval" => ("Chờ duyệt PNK", "pending"),
            "ImportRejected" => ("Từ chối PNK", "rejected"),
            "Received" => ("Đã nhập kho", "received"),
            "Rejected" => ("Đã từ chối", "rejected"),
            _ => ("Chờ duyệt", "pending")
        };
    }
}
