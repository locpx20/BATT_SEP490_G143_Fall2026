using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.PurchaseInvoices
{
    public class PurchaseOrderItemViewModel
    {
        public int InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? SupplierPhone { get; set; }
        public string? SupplierAddress { get; set; }
        public string RawMaterialType { get; set; } = "Mùn cưa cao su & Keo";
        public DateTime PurchaseDate { get; set; }
        public decimal PlannedQuantity { get; set; }
        public decimal WeighedQuantity { get; set; }
        public int ReceiptCount { get; set; }
        public bool HasReceipts => ReceiptCount > 0;
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public int? CreatedByStaffId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public string StatusDisplayName { get; set; } = "Đang thu mua";
        public string StatusBadgeType { get; set; } = "procuring"; // "procuring", "wait_approval", "received", "rejected"
        public string? Note { get; set; }
        public string? RejectionReason { get; set; }
    }

    public class PurchasingStaffOption
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }

    public class PurchaseMaterialOption
    {
        public int MaterialId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal? Price { get; set; }
    }

    public class PurchaseSupplierOption
    {
        public int SupplierId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? StaffFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? DateFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 10;

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        public int TotalItems { get; set; }
        public int TotalPages => PageSize > 0 ? Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize)) : 1;
        public int DisplayFrom => TotalItems == 0 ? 0 : ((CurrentPage - 1) * PageSize + 1);
        public int DisplayTo => Math.Min(CurrentPage * PageSize, TotalItems);

        public List<PurchaseOrderItemViewModel> Orders { get; set; } = new();
        public List<PurchaseOrderItemViewModel> ActiveAssignedInvoices { get; set; } = new();
        public List<PurchasingStaffOption> PurchasingStaffs { get; set; } = new();
        public List<PurchaseMaterialOption> Materials { get; set; } = new();
        public List<PurchaseSupplierOption> ActiveSuppliers { get; set; } = new();

        public decimal CurrentCeilingPrice { get; set; } = 900;
        public int DefaultMaterialId { get; set; }
        public string DefaultMaterialName { get; set; } = "Mùn cưa cao su & Keo";

        public bool CanCreateOrder => IsManager || IsOwner;
        public bool CanCreateSubReceipt => IsPurchasingStaff;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        private const string DefaultMaterialType = "Mùn cưa cao su & Keo";

        private static decimal QuantityOf(PurchaseInvoice p) =>
            p.PurchaseInvoiceDetails.Sum(d => d.Quantity);

        private static string MaterialTypeOf(PurchaseInvoice p)
        {
            var names = p.PurchaseInvoiceDetails
                .Select(d => d.Material?.MaterialName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();
            return names.Count > 0 ? string.Join(", ", names) : DefaultMaterialType;
        }

        private static decimal UnitPriceOf(PurchaseInvoice p)
        {
            var qty = QuantityOf(p);
            return qty > 0 ? Math.Round(p.PurchaseInvoiceDetails.Sum(d => d.Quantity * d.UnitPrice) / qty, 0) : 0;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager, RoleConstants.PurchasingStaff);
            if (check != null) return check;

            if (PageSize <= 0) PageSize = 10;
            if (CurrentPage <= 0) CurrentPage = 1;

            await LoadDataAsync();
            return Page();
        }

        private async Task LoadDataAsync()
        {
            // 1. Tải danh sách nguyên liệu và giá thu mua hiện hành
            Materials = await _context.RawMaterials
                .AsNoTracking()
                .OrderBy(m => m.MaterialName)
                .Select(m => new PurchaseMaterialOption
                {
                    MaterialId = m.MaterialId,
                    Name = m.MaterialName,
                    Unit = m.Unit,
                    Price = m.PurchasePrice
                })
                .ToListAsync();

            var defaultMat = Materials.FirstOrDefault(m => m.Price.HasValue && m.Price > 0)
                             ?? Materials.FirstOrDefault();
            if (defaultMat != null)
            {
                DefaultMaterialId = defaultMat.MaterialId;
                DefaultMaterialName = defaultMat.Name;
                CurrentCeilingPrice = defaultMat.Price ?? 900;
            }

            // 2. Tải danh sách nhà cung cấp đang hoạt động
            ActiveSuppliers = await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.Status == StatusConstants.Active)
                .OrderBy(s => s.SupplierName)
                .Select(s => new PurchaseSupplierOption { SupplierId = s.SupplierId, Name = s.SupplierName })
                .ToListAsync();

            // 3. Tải danh sách nhân viên thu mua để gán và lọc
            var staffQuery = await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .Where(u => u.Status == StatusConstants.Active &&
                            u.Role.RoleName == RoleConstants.PurchasingStaff)
                .OrderBy(u => u.FullName)
                .Select(u => new PurchasingStaffOption
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Phone = u.Phone ?? string.Empty,
                    RoleName = u.Role.RoleName
                })
                .ToListAsync();

            PurchasingStaffs = staffQuery;

            // 4. Tự động chuyển các phiếu tổng quá hạn (hết ngày lập phiếu) sang trạng thái Expired
            await ExpirePastDayInvoicesAsync(_context);

            // 5. Lấy toàn bộ phiếu thu mua
            var query = _context.PurchaseInvoices
                .Include(p => p.Supplier)
                .Include(p => p.CreatedByNavigation)
                .Include(p => p.AssignedToNavigation)
                .Include(p => p.PurchaseReceipts)
                .Include(p => p.PurchaseInvoiceDetails)
                    .ThenInclude(d => d.Material)
                .AsNoTracking()
                .AsQueryable();

            var allInvoices = await query.ToListAsync();

            // Nhân viên thu mua chỉ thấy các phiếu tổng được giao cho mình
            if (IsPurchasingStaff)
            {
                allInvoices = allInvoices.Where(p => (p.AssignedTo ?? p.CreatedBy) == CurrentUserId).ToList();
                ActiveAssignedInvoices = allInvoices
                    .Where(p => !IsInvoiceExpiredByDate(p.PurchaseDate) && p.Status != "Expired" && p.Status != "Rejected")
                    .OrderByDescending(p => p.InvoiceId)
                    .Select(p => new PurchaseOrderItemViewModel
                    {
                        InvoiceId = p.InvoiceId,
                        InvoiceCode = FormatInvoiceBadgeCode(p.InvoiceCode, p.InvoiceId),
                        PlannedQuantity = QuantityOf(p),
                        WeighedQuantity = p.PurchaseReceipts.Where(r => r.Status != "Rejected").Sum(r => r.WeighedQuantity),
                        UnitPrice = UnitPriceOf(p)
                    })
                    .Where(p => p.WeighedQuantity < p.PlannedQuantity)
                    .ToList();
            }

            // Lọc theo ngày
            var filtered = allInvoices.AsEnumerable();
            var now = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(DateFilter) && DateFilter != "all")
            {
                if (DateFilter == "today")
                {
                    var today = now.Date;
                    filtered = filtered.Where(p => p.PurchaseDate.Date == today);
                }
                else if (DateFilter == "week")
                {
                    var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday);
                    filtered = filtered.Where(p => p.PurchaseDate >= startOfWeek);
                }
                else if (DateFilter == "month")
                {
                    var startOfMonth = new DateTime(now.Year, now.Month, 1);
                    filtered = filtered.Where(p => p.PurchaseDate >= startOfMonth);
                }
            }

            // Lọc theo nhân viên thu mua
            if (StaffFilter.HasValue && StaffFilter.Value > 0)
            {
                filtered = filtered.Where(p => (p.AssignedTo ?? p.CreatedBy) == StaffFilter.Value);
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "all")
            {
                var sf = StatusFilter.Trim().ToLowerInvariant();
                filtered = filtered.Where(p =>
                {
                    var plannedQty = QuantityOf(p);
                    var activeReceipts = p.PurchaseReceipts.Where(r => r.Status != "Rejected").ToList();
                    var weighedQty = activeReceipts.Sum(r => r.WeighedQuantity);
                    var receivedQty = activeReceipts.Where(r => r.Status == "Received").Sum(r => r.WeighedQuantity);
                    var (disp, type) = MapStatus(p.Status, p.PurchaseDate, weighedQty, plannedQty, receivedQty);
                    if (sf == "received" || sf == "completed")
                        return type == "received" || type == "completed";
                    return type == sf || (p.Status != null && p.Status.ToLowerInvariant() == sf);
                });
            }

            // Lọc theo từ khóa tìm kiếm
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                filtered = filtered.Where(p =>
                    p.InvoiceCode.ToLowerInvariant().Contains(term) ||
                    (FormatInvoiceBadgeCode(p.InvoiceCode, p.InvoiceId)).ToLowerInvariant().Contains(term) ||
                    (p.Supplier?.SupplierName != null && p.Supplier.SupplierName.ToLowerInvariant().Contains(term)) ||
                    MaterialTypeOf(p).ToLowerInvariant().Contains(term) ||
                    (p.CreatedByNavigation?.FullName != null && p.CreatedByNavigation.FullName.ToLowerInvariant().Contains(term)) ||
                    (NoteTags.Plain(p.Note) != null && NoteTags.Plain(p.Note)!.ToLowerInvariant().Contains(term))
                );
            }

            var mappedList = filtered
                .OrderByDescending(p => p.PurchaseDate)
                .ThenByDescending(p => p.InvoiceId)
                .Select(p =>
                {
                    var plannedQty = QuantityOf(p);
                    var activeReceipts = p.PurchaseReceipts.Where(r => r.Status != "Rejected").ToList();
                    decimal weighedQty = activeReceipts.Sum(r => r.WeighedQuantity);
                    decimal receivedQty = activeReceipts.Where(r => r.Status == "Received").Sum(r => r.WeighedQuantity);
                    var (dispStatus, badgeType) = MapStatus(p.Status, p.PurchaseDate, weighedQty, plannedQty, receivedQty);

                    var badgeCode = FormatInvoiceBadgeCode(p.InvoiceCode, p.InvoiceId);

                    return new PurchaseOrderItemViewModel
                    {
                        InvoiceId = p.InvoiceId,
                        InvoiceCode = badgeCode,
                        SupplierId = p.SupplierId,
                        SupplierName = p.Supplier?.SupplierName ?? "Nhà cung cấp tổng hợp",
                        SupplierPhone = p.Supplier?.Phone,
                        SupplierAddress = p.Supplier?.Address,
                        RawMaterialType = MaterialTypeOf(p),
                        PurchaseDate = p.PurchaseDate,
                        PlannedQuantity = plannedQty,
                        WeighedQuantity = weighedQty,
                        ReceiptCount = p.PurchaseReceipts.Count,
                        UnitPrice = UnitPriceOf(p),
                        TotalAmount = p.TotalAmount,
                        CreatedByStaffId = p.AssignedTo ?? p.CreatedBy,
                        CreatedByName = p.AssignedToNavigation?.FullName ?? p.CreatedByNavigation?.FullName ?? "Chưa phân công",
                        Status = p.Status ?? "Pending",
                        StatusDisplayName = dispStatus,
                        StatusBadgeType = badgeType,
                        Note = NoteTags.Plain(p.Note),
                        RejectionReason = NoteTags.Get(p.Note, NoteTags.Rejected)
                    };
                })
                .ToList();

            TotalItems = mappedList.Count;
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            Orders = mappedList
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }

        public static string FormatInvoiceBadgeCode(string? code, int id)
        {
            if (string.IsNullOrWhiteSpace(code))
                return $"TM-26{id:D3}";

            if (code.StartsWith("TM-", StringComparison.OrdinalIgnoreCase))
                return code.ToUpperInvariant();

            if (code.StartsWith("INV-", StringComparison.OrdinalIgnoreCase))
            {
                // Ví dụ INV-2026-043 -> TM-26043
                var parts = code.Split('-');
                if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 1], out var seq))
                {
                    return $"TM-26{seq:D3}";
                }
            }

            return code;
        }

        public static bool IsInvoiceExpiredByDate(DateTime purchaseDate) =>
            purchaseDate.Date < DateTime.Today;

        public static async Task ExpirePastDayInvoicesAsync(AppDbContext context)
        {
            var today = DateTime.Today;
            var invoices = await context.PurchaseInvoices
                .Include(p => p.PurchaseInvoiceDetails)
                .Include(p => p.PurchaseReceipts)
                .Where(p => p.Status != "Rejected")
                .ToListAsync();

            var changed = false;
            foreach (var inv in invoices)
            {
                var plannedQty = inv.PurchaseInvoiceDetails.Sum(d => d.Quantity);
                var activeReceipts = inv.PurchaseReceipts.Where(r => r.Status != "Rejected").ToList();
                var weighedQty = activeReceipts.Sum(r => r.WeighedQuantity);
                var receivedQty = activeReceipts.Where(r => r.Status == "Received").Sum(r => r.WeighedQuantity);

                string targetStatus;
                if (plannedQty > 0 && receivedQty >= plannedQty)
                {
                    targetStatus = "Received";
                }
                else if (plannedQty > 0 && weighedQty >= plannedQty)
                {
                    targetStatus = "Completed";
                }
                else if (inv.PurchaseDate.Date < today)
                {
                    targetStatus = "Expired";
                }
                else
                {
                    targetStatus = string.IsNullOrWhiteSpace(inv.Status) || inv.Status == "Completed" || inv.Status == "Received" || inv.Status == "Expired"
                        ? "Approved"
                        : inv.Status;
                }

                if (!string.Equals(inv.Status, targetStatus, StringComparison.OrdinalIgnoreCase))
                {
                    inv.Status = targetStatus;
                    changed = true;
                }
            }

            if (changed)
            {
                await context.SaveChangesAsync();
            }
        }

        public static (string DisplayName, string BadgeType) MapStatus(
            string? status,
            DateTime? purchaseDate = null,
            decimal weighedQty = 0m,
            decimal plannedQty = 0m,
            decimal receivedQty = 0m)
        {
            var s = (status ?? "").Trim().ToLowerInvariant();
            if (s == "rejected" || s.Contains("từ chối"))
            {
                return ("Đã từ chối", "rejected");
            }
            if ((plannedQty > 0 && receivedQty >= plannedQty) || s == "received" || s.Contains("nhập kho"))
            {
                return ("Đã nhập kho", "received");
            }
            if ((plannedQty > 0 && weighedQty >= plannedQty) || s == "completed" || s.Contains("hoàn thành") || s.Contains("đủ"))
            {
                return ("Đã thu mua đủ", "completed");
            }
            if (s == "expired" || s.Contains("hết hạn") || (purchaseDate.HasValue && IsInvoiceExpiredByDate(purchaseDate.Value)))
            {
                return ("Đã hết hạn", "expired");
            }
            if (s == "pending" || s == "submitted" || s == "waitownerapproval" || s.Contains("chờ") || s.Contains("owner"))
            {
                return ("Chờ Owner duyệt giá trần", "wait_approval");
            }
            if (s == "approved" || s == "inprogress" || s.Contains("thu mua") || s == "dang thuc hien")
            {
                return ("Đang thu mua", "procuring");
            }
            return ("Đang thu mua", "procuring");
        }

        public async Task<IActionResult> OnPostCreatePurchasePlanAsync(
            decimal plannedWeight,
            decimal ceilingPrice,
            int assignedStaffId,
            int? materialId)
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            IActionResult Back() => RedirectToPage(new
            {
                SearchTerm,
                StatusFilter,
                StaffFilter,
                DateFilter,
                PageSize,
                CurrentPage
            });

            if (plannedWeight <= 0 || plannedWeight > PurchasePricing.MaxOrderQuantity)
            {
                ErrorMessage = $"Khối lượng cần mua phải lớn hơn 0 và không vượt quá {PurchasePricing.MaxOrderQuantity:N0} kg.";
                return Back();
            }

            // Chọn nguyên liệu
            var targetMatId = materialId ?? DefaultMaterialId;
            var material = await _context.RawMaterials.FirstOrDefaultAsync(m => m.MaterialId == targetMatId);
            if (material == null)
            {
                material = await _context.RawMaterials.FirstOrDefaultAsync();
                if (material == null)
                {
                    ErrorMessage = "Hệ thống chưa có danh mục nguyên liệu. Vui lòng kiểm tra lại.";
                    return Back();
                }
            }

            // Giá trần chỉ đọc: luôn lấy từ "Giá Thu mua Nguyên liệu" hiện hành ở server, bỏ qua giá client gửi lên
            if (material.PurchasePrice is not decimal currentPrice || currentPrice <= 0 || currentPrice > PurchasePricing.MaxUnitPrice)
            {
                ErrorMessage = $"Nguyên liệu '{material.MaterialName}' chưa được thiết lập giá thu mua. Vui lòng thiết lập tại mục Giá thu mua Nguyên liệu.";
                return Back();
            }
            ceilingPrice = currentPrice;

            // Chỉ được giao cho người dùng đang hoạt động có vai trò Purchasing Staff
            var assignedUser = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == assignedStaffId);
            if (assignedUser == null || assignedUser.Status != StatusConstants.Active ||
                assignedUser.Role?.RoleName != RoleConstants.PurchasingStaff)
            {
                ErrorMessage = "Nhân viên được phân công phải là nhân viên thu mua (Purchasing Staff) đang hoạt động.";
                return Back();
            }

            // Nhà cung cấp mặc định đang hoạt động
            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Status == StatusConstants.Active);
            if (supplier == null)
            {
                supplier = await _context.Suppliers.FirstOrDefaultAsync();
                if (supplier == null)
                {
                    supplier = new Supplier
                    {
                        SupplierName = "Nhà cung cấp tổng hợp",
                        ContactPerson = "Phòng Mua hàng",
                        Phone = "0900000000",
                        Address = "Nhà máy BATT",
                        Status = StatusConstants.Active,
                        CreatedBy = CurrentUserId
                    };
                    _context.Suppliers.Add(supplier);
                    await _context.SaveChangesAsync();
                }
            }

            // Sinh mã phiếu dạng TM-26xxx
            var yearSuffix = (DateTime.Now.Year % 100).ToString("D2");
            var prefix = $"TM-{yearSuffix}";
            var existingCodes = await _context.PurchaseInvoices
                .Where(p => p.InvoiceCode.StartsWith(prefix) || p.InvoiceCode.StartsWith($"INV-20{yearSuffix}-"))
                .Select(p => p.InvoiceCode)
                .ToListAsync();

            int nextSeq = 1;
            foreach (var c in existingCodes)
            {
                if (c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(c.Substring(prefix.Length), out var n1))
                {
                    if (n1 >= nextSeq) nextSeq = n1 + 1;
                }
                else if (c.StartsWith($"INV-20{yearSuffix}-", StringComparison.OrdinalIgnoreCase) &&
                         int.TryParse(c.Substring($"INV-20{yearSuffix}-".Length), out var n2))
                {
                    if (n2 >= nextSeq) nextSeq = n2 + 1;
                }
            }

            var invoiceCode = $"{prefix}{nextSeq:D3}";
            var totalAmount = Math.Round(plannedWeight * ceilingPrice, 2);

            // Giá trần đã được thiết lập sẵn trong "Giá Thu mua Nguyên liệu" -> không cần duyệt, phiếu vào thẳng "Đang thu mua"
            const string initialStatus = "Approved";

            var invoice = new PurchaseInvoice
            {
                InvoiceCode = invoiceCode,
                SupplierId = supplier.SupplierId,
                PurchaseDate = DateTime.Now,
                TotalAmount = totalAmount,
                Status = initialStatus,
                CreatedBy = CurrentUserId,
                AssignedTo = assignedStaffId,
                Note = $"Phiếu thu mua tổng - Khối lượng dự kiến: {plannedWeight:N0} kg, Giá trần: {ceilingPrice:N0} VNĐ/kg"
            };

            invoice.PurchaseInvoiceDetails.Add(new PurchaseInvoiceDetail
            {
                MaterialId = material.MaterialId,
                Quantity = plannedWeight,
                UnitPrice = ceilingPrice
            });

            _context.PurchaseInvoices.Add(invoice);

            try
            {
                await _context.SaveChangesAsync();
                Notifier.Add(
                    _context,
                    assignedStaffId,
                    $"Phiếu thu mua tổng {invoiceCode} mới",
                    $"Bạn được giao phiếu thu mua tổng {invoiceCode} ({plannedWeight:N0} kg - giá trần {ceilingPrice:N0} VNĐ/kg). Vui lòng liên hệ NCC và tạo đơn mua hàng con.",
                    $"/PurchaseInvoices/Detail?id={invoice.InvoiceId}");
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ErrorMessage = "Không thể tạo phiếu thu mua (có thể trùng mã phiếu). Vui lòng thử lại.";
                return Back();
            }

            var (statusLabel, _) = MapStatus(initialStatus);
            SuccessMessage = $"Đã tạo thành công phiếu thu mua tổng {invoiceCode} ({plannedWeight:N0} kg - {ceilingPrice:N0} VNĐ/kg). Trạng thái: {statusLabel}.";
            return Back();
        }

        public async Task<IActionResult> OnPostUpdatePurchasePlanAsync(
            int invoiceId,
            decimal plannedWeight,
            int assignedStaffId)
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            IActionResult Back() => RedirectToPage(new
            {
                SearchTerm,
                StatusFilter,
                StaffFilter,
                DateFilter,
                PageSize,
                CurrentPage
            });

            var invoice = await _context.PurchaseInvoices
                .Include(p => p.PurchaseReceipts)
                .Include(p => p.PurchaseInvoiceDetails)
                .FirstOrDefaultAsync(p => p.InvoiceId == invoiceId);

            if (invoice == null)
            {
                ErrorMessage = "Không tìm thấy phiếu thu mua tổng.";
                return Back();
            }

            var badgeCode = FormatInvoiceBadgeCode(invoice.InvoiceCode, invoice.InvoiceId);
            if (invoice.PurchaseReceipts.Any())
            {
                ErrorMessage = $"Không thể chỉnh sửa phiếu tổng {badgeCode} vì nhân viên thu mua đã tạo {invoice.PurchaseReceipts.Count} phiếu mua hàng con.";
                return Back();
            }

            if (plannedWeight <= 0 || plannedWeight > PurchasePricing.MaxOrderQuantity)
            {
                ErrorMessage = $"Khối lượng cần mua phải lớn hơn 0 và không vượt quá {PurchasePricing.MaxOrderQuantity:N0} kg.";
                return Back();
            }

            var assignedUser = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == assignedStaffId);
            if (assignedUser == null || assignedUser.Status != StatusConstants.Active ||
                assignedUser.Role?.RoleName != RoleConstants.PurchasingStaff)
            {
                ErrorMessage = "Nhân viên được phân công phải là nhân viên thu mua (Purchasing Staff) đang hoạt động.";
                return Back();
            }

            var detail = invoice.PurchaseInvoiceDetails.FirstOrDefault();
            var unitPrice = detail?.UnitPrice ?? CurrentCeilingPrice;
            if (detail != null)
            {
                detail.Quantity = plannedWeight;
            }

            invoice.AssignedTo = assignedStaffId;
            invoice.TotalAmount = Math.Round(plannedWeight * unitPrice, 2);
            invoice.Note = $"Phiếu thu mua tổng - Khối lượng dự kiến: {plannedWeight:N0} kg, Giá trần: {unitPrice:N0} VNĐ/kg";

            await _context.SaveChangesAsync();
            SuccessMessage = $"Đã cập nhật phiếu thu mua tổng {badgeCode} ({plannedWeight:N0} kg — phân công cho {assignedUser.FullName}).";
            return Back();
        }

        public async Task<IActionResult> OnPostDeletePurchasePlanAsync(int invoiceId)
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            IActionResult Back() => RedirectToPage(new
            {
                SearchTerm,
                StatusFilter,
                StaffFilter,
                DateFilter,
                PageSize,
                CurrentPage
            });

            var invoice = await _context.PurchaseInvoices
                .Include(p => p.PurchaseReceipts)
                .Include(p => p.PurchaseInvoiceDetails)
                .FirstOrDefaultAsync(p => p.InvoiceId == invoiceId);

            if (invoice == null)
            {
                ErrorMessage = "Không tìm thấy phiếu thu mua tổng.";
                return Back();
            }

            var badgeCode = FormatInvoiceBadgeCode(invoice.InvoiceCode, invoice.InvoiceId);
            if (invoice.PurchaseReceipts.Any())
            {
                ErrorMessage = $"Không thể xóa phiếu tổng {badgeCode} vì nhân viên thu mua đã tạo {invoice.PurchaseReceipts.Count} phiếu mua hàng con.";
                return Back();
            }

            if (invoice.PurchaseInvoiceDetails.Any())
            {
                _context.PurchaseInvoiceDetails.RemoveRange(invoice.PurchaseInvoiceDetails);
            }
            _context.PurchaseInvoices.Remove(invoice);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã xóa phiếu thu mua tổng {badgeCode}.";
            return Back();
        }

    }
}
