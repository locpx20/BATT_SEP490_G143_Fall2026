using System.Globalization;
using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.PurchaseInvoices
{
    public class PurchaseReceiptViewModel
    {
        public int ReceiptId { get; set; }
        public string Code { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total => Quantity * Price;
        public string RawStatus { get; set; } = "PendingOrderApproval";
        public string StatusText { get; set; } = string.Empty;
        public string StatusType { get; set; } = "pending"; // pending | warehouse | received | rejected
        public string? RejectionReason { get; set; }
        public string ScalePhoto { get; set; } = string.Empty;
        public string? InvoicePhoto { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Thông tin Phiếu nhập kho nguyên liệu (Receive Goods Receipt information)
        public string? ImportCode { get; set; }
        public string? ImportStatus { get; set; }
        public decimal? ImportedQuantity { get; set; }
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
        public decimal RemainingQuantity => Math.Max(0m, PlannedQuantity - WeighedTotal);
        public string AssignedName { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsExpired { get; set; }

        public List<PurchaseReceiptViewModel> Receipts { get; set; } = new();
        public List<TimelineEntry> Timeline { get; set; } = new();
        public List<PurchaseSupplierOption> Suppliers { get; set; } = new();
        public List<AssignedInvoiceOption> InvoiceOptions { get; set; } = new();

        public bool CanCreateReceipt { get; set; }
        public bool CanManagerReviewOrder => IsManager || IsOwner;
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

        // BƯỚC 2 TRÊN SWIMLANE: Chỉ Purchasing Staff -> Create Purchase Order (hoặc sửa lại sau khi bị từ chối)
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
                ErrorMessage = $"Phiếu tổng {IndexModel.FormatInvoiceBadgeCode(invoice.InvoiceCode, invoice.InvoiceId)} đã hết hạn (chỉ có hiệu lực đến 23:59 ngày {invoice.PurchaseDate:dd/MM/yyyy}). Không thể tạo thêm phiếu mua hàng con.";
                return Back();
            }

            if (IsPurchasingStaff && (invoice.AssignedTo ?? invoice.CreatedBy) != CurrentUserId)
            {
                ErrorMessage = "Phiếu tổng này không được giao cho bạn.";
                return Back();
            }

            if (!TryParsePositiveDecimal(weighedQuantity, out var parsedWeight) ||
                parsedWeight > PurchasePricing.MaxOrderQuantity)
            {
                ErrorMessage = $"Khối lượng cân tại xưởng phải lớn hơn 0 và không vượt quá {PurchasePricing.MaxOrderQuantity:N0} kg.";
                return Back();
            }

            var plannedQty = invoice.PurchaseInvoiceDetails.Sum(d => d.Quantity);
            var existingTotalWeight = await _context.PurchaseReceipts
                .AsNoTracking()
                .Where(r => r.InvoiceId == invoice.InvoiceId
                            && r.Status != "Rejected"
                            && (!editReceiptId.HasValue || r.ReceiptId != editReceiptId.Value))
                .SumAsync(r => r.WeighedQuantity);

            if (plannedQty > 0 && existingTotalWeight + parsedWeight > plannedQty)
            {
                var remainingQty = Math.Max(0m, plannedQty - existingTotalWeight);
                ErrorMessage = $"Tổng khối lượng các phiếu mua con ({existingTotalWeight + parsedWeight:N0} kg) vượt quá khối lượng cần mua của phiếu tổng ({plannedQty:N0} kg). Khối lượng còn lại có thể nhập tối đa là {remainingQty:N0} kg.";
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
                ErrorMessage = $"Giá mua thực tế ({parsedPrice:N0}) không được vượt giá trần của phiếu tổng ({ceiling:N0} VNĐ/kg).";
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
                    ErrorMessage = "Không tìm thấy phiếu mua hàng con cần chỉnh sửa.";
                    return Back();
                }

                var scaleErr = ValidateImage(scalePhoto, required: false, "Ảnh xe cân tại xưởng");
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

            var scaleError = ValidateImage(scalePhoto, required: true, "Ảnh xe cân tại xưởng");
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
                    $"Đơn mua hàng con {code} chờ duyệt",
                    $"{CurrentFullName} vừa tạo đơn mua hàng con {code} ({parsedWeight:N0} kg × {parsedPrice:N0} VNĐ/kg) trên phiếu tổng {IndexModel.FormatInvoiceBadgeCode(invoice.InvoiceCode, invoice.InvoiceId)}, chờ Manager phê duyệt.",
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
                ErrorMessage = $"Không thể tạo phiếu mua hàng con: {ex.InnerException?.Message ?? ex.Message}";
                return Back();
            }

            SuccessMessage = $"Đã tạo đơn mua hàng con {code} ({parsedWeight:N0} kg × {parsedPrice:N0} VNĐ/kg) và gửi cho Manager phê duyệt.";
            return Back();
        }

        // BƯỚC 3A TRÊN SWIMLANE: Manager -> Approved? -> Yes
        public async Task<IActionResult> OnPostApproveOrderAsync(int receiptId)
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Owner);
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
                    $"Đơn mua {receipt.ReceiptCode} đã được Manager duyệt",
                    $"Manager ({CurrentFullName}) đã phê duyệt đơn mua {receipt.ReceiptCode}. Vui lòng bấm 'Tạo yêu cầu nhập kho (Create Inbound Request)' để gửi sang Kho.",
                    $"/PurchaseInvoices/Detail?id={receipt.InvoiceId}");
            }

            await _context.SaveChangesAsync();
            SuccessMessage = $"Đã phê duyệt đơn mua {receipt.ReceiptCode}. Nhân viên thu mua có thể bấm 'Tạo yêu cầu nhập kho (Create Inbound Request)' để gửi sang Kho.";
            return RedirectToPage(new { id = receipt.InvoiceId });
        }

        // BƯỚC 3B TRÊN SWIMLANE: Manager -> Approved? -> No -> Rejection Reason -> quay lại Create Purchase Order
        public async Task<IActionResult> OnPostRejectOrderAsync(int receiptId, string? rejectionReason)
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            var receipt = await _context.PurchaseReceipts.FirstOrDefaultAsync(r => r.ReceiptId == receiptId);
            if (receipt == null) return NotFound();

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                ErrorMessage = "Vui lòng nhập lý do từ chối (Rejection Reason) cho đơn mua hàng.";
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
                    $"Đơn mua {receipt.ReceiptCode} bị từ chối",
                    $"Manager ({CurrentFullName}) đã từ chối đơn mua {receipt.ReceiptCode}. Lý do: {reason}. Vui lòng bấm 'Sửa & Gửi lại đơn mua'.",
                    $"/PurchaseInvoices/Detail?id={receipt.InvoiceId}");
            }

            await _context.SaveChangesAsync();
            SuccessMessage = $"Đã từ chối đơn mua {receipt.ReceiptCode} kèm lý do và gửi trả về cho Nhân viên thu mua chỉnh sửa.";
            return RedirectToPage(new { id = receipt.InvoiceId });
        }

        // BƯỚC 4 TRÊN SWIMLANE: Purchasing Staff -> Create Inbound Request -> Warehouse Manager: Inbound Request Notification
        public async Task<IActionResult> OnPostCreateInboundRequestAsync(int receiptId)
        {
            var check = RequireRole(RoleConstants.PurchasingStaff);
            if (check != null) return check;

            var receipt = await _context.PurchaseReceipts
                .Include(r => r.Supplier)
                .FirstOrDefaultAsync(r => r.ReceiptId == receiptId);
            if (receipt == null) return NotFound();

            if (receipt.Status != "OrderApproved")
            {
                ErrorMessage = $"Đơn mua {receipt.ReceiptCode} cần được Manager phê duyệt trước khi tạo yêu cầu nhập kho.";
                return RedirectToPage(new { id = receipt.InvoiceId });
            }

            receipt.Status = "WaitWarehouse";
            receipt.RejectionReason = null;

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
                    $"Yêu cầu nhập kho nguyên liệu mới ({receipt.ReceiptCode})",
                    $"Nhân viên thu mua ({CurrentFullName}) đã gửi Yêu cầu nhập kho (Inbound Request) cho phiếu {receipt.ReceiptCode} — {receipt.WeighedQuantity:N0} kg từ {receipt.Supplier?.SupplierName}. Vui lòng kiểm tra đối chiếu và lập Phiếu nhập kho nguyên liệu.",
                    "/Inventory/Index?tab=raw");
            }

            await _context.SaveChangesAsync();
            SuccessMessage = $"Đã tạo Yêu cầu nhập kho (Inbound Request) cho phiếu {receipt.ReceiptCode} và gửi thông báo (Inbound Request Notification) tới Thủ kho.";
            return RedirectToPage(new { id = receipt.InvoiceId });
        }

        private async Task NotifyManagersAsync(string title, string message, string url)
        {
            var managerIds = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Status == StatusConstants.Active &&
                            (u.Role.RoleName == RoleConstants.Manager || u.Role.RoleName == RoleConstants.Owner))
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
                .FirstOrDefaultAsync(p => p.InvoiceId == id);
            if (invoice == null) return false;

            var assignedId = invoice.AssignedTo ?? invoice.CreatedBy;
            if (IsPurchasingStaff && assignedId != CurrentUserId) return false;

            var receiptIds = invoice.PurchaseReceipts.Select(r => r.ReceiptId).ToList();
            var imports = await _context.RawMaterialImports
                .AsNoTracking()
                .Include(i => i.ImportedByNavigation)
                .Include(i => i.ApprovedByNavigation)
                .Where(i => i.ReceiptId.HasValue && receiptIds.Contains(i.ReceiptId.Value))
                .OrderByDescending(i => i.ImportId)
                .ToListAsync();

            var importByReceiptId = imports
                .GroupBy(i => i.ReceiptId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            InvoiceId = invoice.InvoiceId;
            InvoiceCode = IndexModel.FormatInvoiceBadgeCode(invoice.InvoiceCode, invoice.InvoiceId);
            PlannedQuantity = invoice.PurchaseInvoiceDetails.Sum(d => d.Quantity);
            CeilingPrice = invoice.PurchaseInvoiceDetails.Select(d => d.UnitPrice).DefaultIfEmpty(0).Max();
            var activeReceipts = invoice.PurchaseReceipts.Where(r => r.Status != "Rejected").ToList();
            var weighedQty = activeReceipts.Sum(r => r.WeighedQuantity);
            var receivedQty = activeReceipts.Where(r => r.Status == "Received").Sum(r => r.WeighedQuantity);
            var (statusText, statusType) = IndexModel.MapStatus(invoice.Status, invoice.PurchaseDate, weighedQty, PlannedQuantity, receivedQty);
            StatusText = statusText;
            StatusType = statusType;
            AssignedName = invoice.AssignedToNavigation?.FullName ?? invoice.CreatedByNavigation?.FullName ?? "—";
            CreatorName = invoice.CreatedByNavigation?.FullName ?? "—";
            CreatedAt = invoice.PurchaseDate;
            ExpiresAt = invoice.PurchaseDate.Date.AddDays(1).AddSeconds(-1);
            IsExpired = IndexModel.IsInvoiceExpiredByDate(invoice.PurchaseDate) ||
                        string.Equals(invoice.Status, "Expired", StringComparison.OrdinalIgnoreCase);

            Receipts = invoice.PurchaseReceipts
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.ReceiptId)
                .Select(r =>
                {
                    var (text, type) = ReceiptStatus(r.Status);
                    importByReceiptId.TryGetValue(r.ReceiptId, out var imp);
                    var impCode = imp != null
                        ? (string.IsNullOrWhiteSpace(imp.ImportCode) ? $"PNK-26{imp.ImportId:D3}" : imp.ImportCode)
                        : null;

                    return new PurchaseReceiptViewModel
                    {
                        ReceiptId = r.ReceiptId,
                        Code = r.ReceiptCode,
                        SupplierId = r.SupplierId,
                        SupplierName = r.Supplier?.SupplierName ?? "—",
                        Quantity = r.WeighedQuantity,
                        Price = r.ActualPrice,
                        RawStatus = r.Status ?? "PendingOrderApproval",
                        StatusText = text,
                        StatusType = type,
                        RejectionReason = r.RejectionReason,
                        ScalePhoto = r.ScalePhotoPath,
                        InvoicePhoto = r.InvoicePhotoPath,
                        CreatedByName = r.CreatedByNavigation?.FullName ?? "—",
                        CreatedAt = r.CreatedAt,
                        ImportCode = impCode,
                        ImportStatus = imp?.Status,
                        ImportedQuantity = imp?.Quantity,
                        ImportedByName = imp?.ImportedByNavigation?.FullName,
                        ImportDate = imp?.ImportDate,
                        ApprovedByName = imp?.ApprovedByNavigation?.FullName,
                        ApprovedAt = imp?.ApprovedAt
                    };
                })
                .ToList();
            WeighedTotal = Receipts.Sum(r => r.Quantity);

            Timeline = Receipts
                .Select(r => new TimelineEntry { At = r.CreatedAt, Text = $"{r.CreatedByName} tạo đơn mua con {r.Code} ({r.StatusText})" })
                .Append(new TimelineEntry { At = invoice.PurchaseDate, Text = $"{CreatorName} tạo phiếu tổng {InvoiceCode}" })
                .OrderByDescending(t => t.At)
                .ToList();

            // Chỉ Nhân viên thu mua (Purchasing Staff) được giao phiếu mới tạo phiếu mua hàng con; Manager không tạo phiếu con
            CanCreateReceipt = !IsExpired &&
                               invoice.Status != "Rejected" &&
                               IsPurchasingStaff &&
                               assignedId == CurrentUserId;

            Suppliers = await _context.Suppliers.AsNoTracking()
                .Where(s => s.Status == StatusConstants.Active)
                .OrderBy(s => s.SupplierName)
                .Select(s => new PurchaseSupplierOption { SupplierId = s.SupplierId, Name = s.SupplierName })
                .ToListAsync();

            var today = DateTime.Today;
            var optionQuery = _context.PurchaseInvoices.AsNoTracking()
                .Where(p => p.Status != "Rejected" && p.Status != "Expired" && p.PurchaseDate >= today);
            if (IsPurchasingStaff)
                optionQuery = optionQuery.Where(p => (p.AssignedTo ?? p.CreatedBy) == CurrentUserId);

            InvoiceOptions = (await optionQuery.OrderByDescending(p => p.InvoiceId).ToListAsync())
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
