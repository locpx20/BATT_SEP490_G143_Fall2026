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
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public int CreatedByStaffId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public int? ApprovedByStaffId { get; set; }
        public string? ApprovedByName { get; set; }
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
        public List<PurchasingStaffOption> PurchasingStaffs { get; set; } = new();
        public List<PurchaseMaterialOption> Materials { get; set; } = new();
        public List<PurchaseSupplierOption> ActiveSuppliers { get; set; } = new();

        public decimal CurrentCeilingPrice { get; set; } = 900;
        public int DefaultMaterialId { get; set; }
        public string DefaultMaterialName { get; set; } = "Mùn cưa cao su & Keo";

        public bool CanApprove => IsOwner;
        public bool CanCreateOrder => IsPurchasingStaff || IsManager || IsOwner;

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
                            (u.Role.RoleName == RoleConstants.PurchasingStaff ||
                             u.Role.RoleName == RoleConstants.Manager))
                .OrderBy(u => u.FullName)
                .Select(u => new PurchasingStaffOption
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Phone = u.Phone ?? string.Empty,
                    RoleName = (u.Role != null ? (u.Role.Description ?? u.Role.RoleName) : "Nhân viên thu mua")
                })
                .ToListAsync();

            // Đảm bảo luôn có nhân viên để chọn (nếu DB chưa có ai thì fallback user hiện tại hoặc mẫu)
            if (staffQuery.Count == 0)
            {
                var currentUser = await _context.Users.FindAsync(CurrentUserId);
                if (currentUser != null)
                {
                    staffQuery.Add(new PurchasingStaffOption
                    {
                        UserId = currentUser.UserId,
                        FullName = currentUser.FullName ?? "Người dùng",
                        Phone = currentUser.Phone ?? string.Empty,
                        RoleName = "Nhân viên thu mua"
                    });
                }
                else
                {
                    staffQuery.Add(new PurchasingStaffOption
                    {
                        UserId = 1,
                        FullName = "Phạm Thị Mai",
                        Phone = "0908888999",
                        RoleName = "Nhân viên thu mua"
                    });
                }
            }

            PurchasingStaffs = staffQuery;

            // 4. Lấy toàn bộ phiếu thu mua
            var query = _context.PurchaseInvoices
                .Include(p => p.Supplier)
                .Include(p => p.CreatedByNavigation)
                .Include(p => p.ApprovedByNavigation)
                .Include(p => p.PurchaseInvoiceDetails)
                    .ThenInclude(d => d.Material)
                .AsNoTracking()
                .AsQueryable();

            var allInvoices = await query.ToListAsync();

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
                filtered = filtered.Where(p => p.CreatedBy == StaffFilter.Value);
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "all")
            {
                var sf = StatusFilter.Trim().ToLowerInvariant();
                filtered = filtered.Where(p =>
                {
                    var (disp, type) = MapStatus(p.Status);
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
                    var (dispStatus, badgeType) = MapStatus(p.Status);

                    // Đã cân: nếu đã duyệt/nhập kho thì là khối lượng cân thực tế hoặc tương đương
                    decimal weighedQty = 0;
                    if (badgeType == "received")
                    {
                        weighedQty = plannedQty;
                    }
                    else if (badgeType == "procuring")
                    {
                        weighedQty = plannedQty > 0 ? plannedQty : 0;
                    }
                    else
                    {
                        weighedQty = 0;
                    }

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
                        UnitPrice = UnitPriceOf(p),
                        TotalAmount = p.TotalAmount,
                        CreatedByStaffId = p.CreatedBy,
                        CreatedByName = p.CreatedByNavigation?.FullName ?? "Phạm Thị Mai",
                        ApprovedByStaffId = p.ApprovedBy,
                        ApprovedByName = p.ApprovedByNavigation?.FullName,
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

        public static (string DisplayName, string BadgeType) MapStatus(string? status)
        {
            var s = (status ?? "").Trim().ToLowerInvariant();
            if (s == "pending" || s == "submitted" || s == "waitownerapproval" || s.Contains("chờ") || s.Contains("owner"))
            {
                return ("Chờ Owner duyệt giá trần", "wait_approval");
            }
            if (s == "approved" || s == "inprogress" || s.Contains("thu mua") || s == "dang thuc hien")
            {
                return ("Đang thu mua", "procuring");
            }
            if (s == "received" || s == "completed" || s.Contains("nhập kho"))
            {
                return ("Đã nhập kho", "received");
            }
            if (s == "rejected" || s.Contains("từ chối"))
            {
                return ("Đã từ chối", "rejected");
            }
            return ("Đang thu mua", "procuring");
        }

        public async Task<IActionResult> OnPostCreatePurchasePlanAsync(
            decimal plannedWeight,
            decimal ceilingPrice,
            int assignedStaffId,
            int? materialId)
        {
            var check = RequireRole(RoleConstants.PurchasingStaff, RoleConstants.Manager, RoleConstants.Owner);
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

            if (ceilingPrice <= 0 || ceilingPrice > PurchasePricing.MaxUnitPrice)
            {
                ErrorMessage = $"Giá trần phải lớn hơn 0 và không vượt quá {PurchasePricing.MaxUnitPrice:N0} VNĐ/kg.";
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

            // Kiểm tra nhân viên được giao
            var assignedUser = await _context.Users.FindAsync(assignedStaffId);
            if (assignedUser == null)
            {
                assignedStaffId = CurrentUserId;
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

            // Xác định trạng thái: Nếu giá trần cao hơn giá thu mua niêm yết -> Chờ Owner duyệt giá trần
            // Nếu bằng hoặc thấp hơn -> Đang thu mua
            string initialStatus = "Pending";
            var standardPrice = material.PurchasePrice ?? 0;
            if (standardPrice > 0 && ceilingPrice <= standardPrice)
            {
                initialStatus = "Approved"; // Đang thu mua
            }
            else
            {
                initialStatus = "Pending"; // Chờ Owner duyệt giá trần
            }

            var invoice = new PurchaseInvoice
            {
                InvoiceCode = invoiceCode,
                SupplierId = supplier.SupplierId,
                PurchaseDate = DateTime.Now,
                TotalAmount = totalAmount,
                Status = initialStatus,
                CreatedBy = assignedStaffId,
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

        public async Task<IActionResult> OnPostApproveOrderAsync(int invoiceId)
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (!IsOwner)
            {
                ErrorMessage = "Chỉ Giám đốc (Owner) mới có quyền duyệt giá trần phiếu thu mua.";
                return RedirectToPage(new { SearchTerm, StatusFilter, StaffFilter, DateFilter, PageSize, CurrentPage });
            }

            var invoice = await _context.PurchaseInvoices.FindAsync(invoiceId);
            if (invoice == null)
            {
                ErrorMessage = "Không tìm thấy phiếu thu mua cần duyệt.";
                return RedirectToPage(new { SearchTerm, StatusFilter, StaffFilter, DateFilter, PageSize, CurrentPage });
            }

            invoice.Status = "Approved"; // Đang thu mua
            invoice.ApprovedBy = CurrentUserId;
            invoice.Note = NoteTags.Remove(invoice.Note, NoteTags.Rejected);
            await _context.SaveChangesAsync();

            var badgeCode = FormatInvoiceBadgeCode(invoice.InvoiceCode, invoice.InvoiceId);
            SuccessMessage = $"Đã phê duyệt giá trần cho phiếu thu mua {badgeCode}. Phiếu đã chuyển sang trạng thái 'Đang thu mua'.";
            return RedirectToPage(new { SearchTerm, StatusFilter, StaffFilter, DateFilter, PageSize, CurrentPage });
        }

        public async Task<IActionResult> OnPostRejectOrderAsync(int invoiceId, string rejectionReason)
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (!IsOwner)
            {
                ErrorMessage = "Chỉ Giám đốc (Owner) mới có quyền từ chối phiếu thu mua.";
                return RedirectToPage(new { SearchTerm, StatusFilter, StaffFilter, DateFilter, PageSize, CurrentPage });
            }

            var invoice = await _context.PurchaseInvoices.FindAsync(invoiceId);
            if (invoice == null)
            {
                ErrorMessage = "Không tìm thấy phiếu thu mua.";
                return RedirectToPage(new { SearchTerm, StatusFilter, StaffFilter, DateFilter, PageSize, CurrentPage });
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                ErrorMessage = "Vui lòng nhập lý do từ chối phiếu thu mua.";
                return RedirectToPage(new { SearchTerm, StatusFilter, StaffFilter, DateFilter, PageSize, CurrentPage });
            }

            invoice.Status = "Rejected";
            invoice.ApprovedBy = CurrentUserId;
            invoice.Note = NoteTags.Set(invoice.Note, NoteTags.Rejected, rejectionReason.Trim());
            await _context.SaveChangesAsync();

            var badgeCode = FormatInvoiceBadgeCode(invoice.InvoiceCode, invoice.InvoiceId);
            SuccessMessage = $"Đã từ chối phiếu thu mua {badgeCode}. Lý do: {rejectionReason.Trim()}";
            return RedirectToPage(new { SearchTerm, StatusFilter, StaffFilter, DateFilter, PageSize, CurrentPage });
        }
    }
}
