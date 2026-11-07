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
        public string SupplierName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total => Quantity * Price;
        public string StatusText { get; set; } = string.Empty;
        public string StatusType { get; set; } = "pending"; // pending | warehouse | received
        public string ScalePhoto { get; set; } = string.Empty;
        public string? InvoicePhoto { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
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
        public string AssignedName { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsExpired { get; set; }

        public List<PurchaseReceiptViewModel> Receipts { get; set; } = new();
        public List<TimelineEntry> Timeline { get; set; } = new();
        public List<PurchaseSupplierOption> Suppliers { get; set; } = new();
        public List<AssignedInvoiceOption> InvoiceOptions { get; set; } = new();

        // Nhân viên thu mua (được giao), Quản lý hoặc Owner có thể nhập nhiều phiếu con khi phiếu tổng còn hạn trong ngày
        public bool CanCreateReceipt { get; set; }

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

        public async Task<IActionResult> OnPostCreateReceiptAsync(
            int invoiceId,
            int supplierId,
            string? weighedQuantity,
            string? actualPrice,
            IFormFile? scalePhoto,
            IFormFile? invoicePhoto)
        {
            var check = RequireRole(RoleConstants.PurchasingStaff, RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            var targetId = invoiceId > 0 ? invoiceId : Id;
            IActionResult Back() => RedirectToPage(new { id = targetId });

            await IndexModel.ExpirePastDayInvoicesAsync(_context);

            var invoice = await _context.PurchaseInvoices
                .AsNoTracking()
                .Include(p => p.PurchaseInvoiceDetails)
                .FirstOrDefaultAsync(p => p.InvoiceId == targetId);
            if (invoice == null) return NotFound();

            // Kiểm tra hết hạn (hết ngày tính từ lúc lập phiếu)
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

            var scaleError = ValidateImage(scalePhoto, required: true, "Ảnh xe cân tại xưởng");
            if (scaleError != null) { ErrorMessage = scaleError; return Back(); }
            var invoiceImgError = ValidateImage(invoicePhoto, required: false, "Hóa đơn");
            if (invoiceImgError != null) { ErrorMessage = invoiceImgError; return Back(); }

            // Mã phiếu con PM-26xxx, nối tiếp mã lớn nhất của năm
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
                Status = "Pending",
                CreatedBy = CurrentUserId,
                CreatedAt = DateTime.Now
            });

            try
            {
                await _context.SaveChangesAsync();
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

            SuccessMessage = $"Đã tạo phiếu mua hàng con {code}: {parsedWeight:N0} kg × {parsedPrice:N0} VNĐ/kg. Phiếu đang chờ kho xác nhận.";
            return Back();
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

            InvoiceId = invoice.InvoiceId;
            InvoiceCode = IndexModel.FormatInvoiceBadgeCode(invoice.InvoiceCode, invoice.InvoiceId);
            var (statusText, statusType) = IndexModel.MapStatus(invoice.Status, invoice.PurchaseDate);
            StatusText = statusText;
            StatusType = statusType;
            PlannedQuantity = invoice.PurchaseInvoiceDetails.Sum(d => d.Quantity);
            CeilingPrice = invoice.PurchaseInvoiceDetails.Select(d => d.UnitPrice).DefaultIfEmpty(0).Max();
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
                    return new PurchaseReceiptViewModel
                    {
                        ReceiptId = r.ReceiptId,
                        Code = r.ReceiptCode,
                        SupplierName = r.Supplier?.SupplierName ?? "—",
                        Quantity = r.WeighedQuantity,
                        Price = r.ActualPrice,
                        StatusText = text,
                        StatusType = type,
                        ScalePhoto = r.ScalePhotoPath,
                        InvoicePhoto = r.InvoicePhotoPath,
                        CreatedByName = r.CreatedByNavigation?.FullName ?? "—",
                        CreatedAt = r.CreatedAt
                    };
                })
                .ToList();
            WeighedTotal = Receipts.Sum(r => r.Quantity);

            Timeline = Receipts
                .Select(r => new TimelineEntry { At = r.CreatedAt, Text = $"{r.CreatedByName} tạo phiếu cân {r.Code}" })
                .Append(new TimelineEntry { At = invoice.PurchaseDate, Text = $"{CreatorName} tạo phiếu tổng {InvoiceCode}" })
                .OrderByDescending(t => t.At)
                .ToList();

            CanCreateReceipt = !IsExpired &&
                               invoice.Status != "Rejected" &&
                               (IsOwner || IsManager || (IsPurchasingStaff && assignedId == CurrentUserId));

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

        public static (string Text, string Type) ReceiptStatus(string? status) => (status ?? "Pending") switch
        {
            "Received" => ("Đã nhập kho", "received"),
            "WaitManagerApproval" => ("Chờ Manager duyệt nhập kho", "warehouse"),
            "WaitWarehouse" => ("Chờ kho xác nhận nhập", "warehouse"),
            "Rejected" => ("Đã từ chối", "rejected"),
            _ => ("Chờ duyệt cân", "pending")
        };
    }
}
