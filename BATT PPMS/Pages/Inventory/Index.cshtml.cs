using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Inventory
{
    public class WarehouseReceiptRowViewModel
    {
        public int ReceiptId { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public int InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public string MaterialName { get; set; } = "Nguyên liệu thô";
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal WeighedQuantity { get; set; }
        public decimal ActualPrice { get; set; }
        public decimal TotalAmount => WeighedQuantity * ActualPrice;
        public string Status { get; set; } = "Pending";
        public string StatusText { get; set; } = "Chờ duyệt cân";
        public string StatusType { get; set; } = "pending"; // pending | warehouse | received | rejected
        public string ScalePhotoPath { get; set; } = string.Empty;
        public string? InvoicePhotoPath { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Thông tin phiếu nhập kho nguyên liệu liên kết (nếu kho đã xác nhận)
        public int? ImportId { get; set; }
        public string? ImportCode { get; set; }
        public string? ImportStatus { get; set; }
        public string? ImportedByName { get; set; }
        public DateTime? ImportDate { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }

    public class RawMaterialImportRowViewModel
    {
        public int ImportId { get; set; }
        public string ImportCode { get; set; } = string.Empty;
        public int? ReceiptId { get; set; }
        public string ReceiptCode { get; set; } = "—";
        public string InvoiceCode { get; set; } = "—";
        public string MaterialName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount => Quantity * UnitPrice;
        public DateTime ImportDate { get; set; }
        public string ImportedByName { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public string StatusText { get; set; } = "Chờ Manager duyệt";
        public string StatusType { get; set; } = "warehouse"; // warehouse | received | rejected
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ScalePhotoPath { get; set; }
        public string? InvoicePhotoPath { get; set; }
    }

    public class SupplierFilterOption
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
        public string Tab { get; set; } = "raw";

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SupplierFilter { get; set; }

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

        public List<RawMaterial> Materials { get; set; } = new();
        public List<FinishedGoodBatch> Batches { get; set; } = new();
        public PageWindow Pagination { get; set; } = new(0, 1);

        public List<WarehouseReceiptRowViewModel> Receipts { get; set; } = new();
        public List<RawMaterialImportRowViewModel> ImportTickets { get; set; } = new();
        public List<SupplierFilterOption> Suppliers { get; set; } = new();

        // KPI
        public decimal TotalRawInventoryKg { get; set; }
        public int PendingWarehouseCount { get; set; }
        public int WaitingManagerApprovalCount { get; set; }
        public int ReceivedReceiptsCount { get; set; }
        public decimal TotalWeighedKg { get; set; }

        // Phân quyền thao tác:
        // - Thủ kho xác nhận nhập kho -> tạo phiếu nhập kho nguyên liệu gửi Manager
        // - Manager / Owner duyệt phiếu nhập kho nguyên liệu -> cộng tồn kho
        public bool CanWarehouseConfirm => IsWarehouseStaff || IsManager || IsOwner;
        public bool CanManagerApprove => IsManager || IsOwner;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(
                RoleConstants.WarehouseStaff,
                RoleConstants.Manager,
                RoleConstants.Owner,
                RoleConstants.PurchasingStaff);
            if (check != null) return check;

            if (Tab != "finished") Tab = "raw";
            var page = PageWindow.ReadRequestedPage(Request.Query, PageNumber);

            if (Tab == "finished")
            {
                var query = _context.FinishedGoodBatches
                    .AsNoTracking()
                    .Include(x => x.CreatedByNavigation)
                    .Where(x => x.Status == "In Stock");
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                    query = query.Where(x => x.BagNumber.Contains(SearchTerm.Trim()));
                Pagination = new(await query.CountAsync(), page);
                Batches = await query
                    .OrderByDescending(x => x.ProductionDate)
                    .ThenByDescending(x => x.BatchId)
                    .Skip(Pagination.Offset)
                    .Take(PageWindow.Size)
                    .ToListAsync();
                return Page();
            }

            if (PageSize <= 0) PageSize = 10;
            if (CurrentPage <= 0) CurrentPage = 1;

            var matQuery = _context.RawMaterials.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(SearchTerm))
                matQuery = matQuery.Where(x => x.MaterialName.Contains(SearchTerm.Trim()));
            Pagination = new(await matQuery.CountAsync(), page);
            Materials = await matQuery
                .OrderBy(x => x.MaterialName)
                .Skip(Pagination.Offset)
                .Take(PageWindow.Size)
                .ToListAsync();

            await LoadDataAsync();
            return Page();
        }

        private async Task LoadDataAsync()
        {
            TotalRawInventoryKg = await _context.RawMaterials
                .AsNoTracking()
                .SumAsync(m => (decimal?)m.CurrentQuantity) ?? 0m;

            Suppliers = await _context.Suppliers
                .AsNoTracking()
                .OrderBy(s => s.SupplierName)
                .Select(s => new SupplierFilterOption
                {
                    SupplierId = s.SupplierId,
                    Name = s.SupplierName
                })
                .ToListAsync();

            var allImports = await _context.RawMaterialImports
                .AsNoTracking()
                .Include(i => i.Material)
                .Include(i => i.Supplier)
                .Include(i => i.ImportedByNavigation)
                .Include(i => i.ApprovedByNavigation)
                .Include(i => i.Receipt)
                    .ThenInclude(r => r!.Invoice)
                .OrderByDescending(i => i.ImportDate)
                .ThenByDescending(i => i.ImportId)
                .ToListAsync();

            var importByReceiptId = allImports
                .Where(i => i.ReceiptId.HasValue)
                .GroupBy(i => i.ReceiptId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            ImportTickets = allImports.Select(i =>
            {
                var st = (i.Status ?? "Pending").Trim();
                var (stText, stType) = st switch
                {
                    "Approved" => ("Đã duyệt nhập kho", "received"),
                    "Rejected" => ("Đã từ chối", "rejected"),
                    _ => ("Chờ Manager duyệt", "warehouse")
                };

                var invCode = i.Receipt?.Invoice != null
                    ? BATT_PPMS.Pages.PurchaseInvoices.IndexModel.FormatInvoiceBadgeCode(i.Receipt.Invoice.InvoiceCode, i.Receipt.Invoice.InvoiceId)
                    : "—";

                return new RawMaterialImportRowViewModel
                {
                    ImportId = i.ImportId,
                    ImportCode = string.IsNullOrWhiteSpace(i.ImportCode) ? $"PNK-26{i.ImportId:D3}" : i.ImportCode!,
                    ReceiptId = i.ReceiptId,
                    ReceiptCode = i.Receipt?.ReceiptCode ?? "—",
                    InvoiceCode = invCode,
                    MaterialName = i.Material?.MaterialName ?? "Nguyên liệu thô",
                    SupplierName = i.Supplier?.SupplierName ?? "—",
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    ImportDate = i.ImportDate,
                    ImportedByName = i.ImportedByNavigation?.FullName ?? "—",
                    Status = st,
                    StatusText = stText,
                    StatusType = stType,
                    ApprovedByName = i.ApprovedByNavigation?.FullName,
                    ApprovedAt = i.ApprovedAt,
                    ScalePhotoPath = i.Receipt?.ScalePhotoPath,
                    InvoicePhotoPath = i.Receipt?.InvoicePhotoPath
                };
            }).ToList();

            var allReceipts = await _context.PurchaseReceipts
                .AsNoTracking()
                .Include(r => r.Supplier)
                .Include(r => r.CreatedByNavigation)
                .Include(r => r.Invoice)
                    .ThenInclude(i => i.PurchaseInvoiceDetails)
                        .ThenInclude(d => d.Material)
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.ReceiptId)
                .ToListAsync();

            PendingWarehouseCount = allReceipts.Count(r => r.Status == null || r.Status == "Pending" || r.Status == "WaitWarehouse");
            WaitingManagerApprovalCount = allReceipts.Count(r => r.Status == "WaitManagerApproval") +
                                          allImports.Count(i => i.Status == "Pending" && !i.ReceiptId.HasValue);
            ReceivedReceiptsCount = allReceipts.Count(r => r.Status == "Received");
            TotalWeighedKg = allReceipts.Sum(r => r.WeighedQuantity);

            var mapped = allReceipts.Select(r =>
            {
                var (statusText, statusType) = BATT_PPMS.Pages.PurchaseInvoices.DetailModel.ReceiptStatus(r.Status);
                var invoiceCode = r.Invoice != null
                    ? BATT_PPMS.Pages.PurchaseInvoices.IndexModel.FormatInvoiceBadgeCode(r.Invoice.InvoiceCode, r.Invoice.InvoiceId)
                    : $"TM-26{r.InvoiceId:D3}";

                var matName = r.Invoice?.PurchaseInvoiceDetails
                    .Select(d => d.Material?.MaterialName)
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "Nguyên liệu thô";

                importByReceiptId.TryGetValue(r.ReceiptId, out var linkedImport);

                return new WarehouseReceiptRowViewModel
                {
                    ReceiptId = r.ReceiptId,
                    ReceiptCode = r.ReceiptCode,
                    InvoiceId = r.InvoiceId,
                    InvoiceCode = invoiceCode,
                    MaterialName = matName,
                    SupplierId = r.SupplierId,
                    SupplierName = r.Supplier?.SupplierName ?? "—",
                    WeighedQuantity = r.WeighedQuantity,
                    ActualPrice = r.ActualPrice,
                    Status = r.Status ?? "Pending",
                    StatusText = statusText,
                    StatusType = statusType,
                    ScalePhotoPath = r.ScalePhotoPath,
                    InvoicePhotoPath = r.InvoicePhotoPath,
                    CreatedByName = r.CreatedByNavigation?.FullName ?? "—",
                    CreatedAt = r.CreatedAt,
                    ImportId = linkedImport?.ImportId,
                    ImportCode = linkedImport != null
                        ? (string.IsNullOrWhiteSpace(linkedImport.ImportCode) ? $"PNK-26{linkedImport.ImportId:D3}" : linkedImport.ImportCode)
                        : null,
                    ImportStatus = linkedImport?.Status,
                    ImportedByName = linkedImport?.ImportedByNavigation?.FullName,
                    ImportDate = linkedImport?.ImportDate,
                    ApprovedByName = linkedImport?.ApprovedByNavigation?.FullName,
                    ApprovedAt = linkedImport?.ApprovedAt
                };
            }).AsEnumerable();

            // Lọc theo ngày
            var now = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(DateFilter) && DateFilter != "all")
            {
                if (DateFilter == "today")
                {
                    var today = now.Date;
                    mapped = mapped.Where(r => r.CreatedAt.Date == today);
                }
                else if (DateFilter == "week")
                {
                    var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday);
                    mapped = mapped.Where(r => r.CreatedAt >= startOfWeek);
                }
                else if (DateFilter == "month")
                {
                    var startOfMonth = new DateTime(now.Year, now.Month, 1);
                    mapped = mapped.Where(r => r.CreatedAt >= startOfMonth);
                }
            }

            // Lọc theo nhà cung cấp
            if (SupplierFilter.HasValue && SupplierFilter.Value > 0)
            {
                mapped = mapped.Where(r => r.SupplierId == SupplierFilter.Value);
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "all")
            {
                var sf = StatusFilter.Trim().ToLowerInvariant();
                if (sf == "pending")
                {
                    mapped = mapped.Where(r => r.Status == "Pending" || r.Status == "WaitWarehouse");
                }
                else if (sf == "wait_manager")
                {
                    mapped = mapped.Where(r => r.Status == "WaitManagerApproval");
                }
                else if (sf == "received")
                {
                    mapped = mapped.Where(r => r.Status == "Received");
                }
            }

            // Lọc theo từ khóa
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                mapped = mapped.Where(r =>
                    r.ReceiptCode.ToLowerInvariant().Contains(term) ||
                    r.InvoiceCode.ToLowerInvariant().Contains(term) ||
                    (r.ImportCode != null && r.ImportCode.ToLowerInvariant().Contains(term)) ||
                    r.SupplierName.ToLowerInvariant().Contains(term) ||
                    r.CreatedByName.ToLowerInvariant().Contains(term) ||
                    r.MaterialName.ToLowerInvariant().Contains(term));
            }

            var finalList = mapped.ToList();
            TotalItems = finalList.Count;
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            Receipts = finalList
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }

        // BƯỚC 1: Thủ kho (Warehouse) xác nhận nhập kho -> Tạo Phiếu nhập kho nguyên liệu (PNK-26xxx) ở trạng thái Pending và gửi cho Manager approve
        public async Task<IActionResult> OnPostConfirmReceiptAsync(int receiptId)
        {
            var check = RequireRole(RoleConstants.WarehouseStaff, RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            IActionResult Back() => RedirectToPage(new
            {
                Tab = "raw",
                SearchTerm,
                StatusFilter,
                SupplierFilter,
                DateFilter,
                PageSize,
                CurrentPage
            });

            var receipt = await _context.PurchaseReceipts
                .Include(r => r.Invoice)
                    .ThenInclude(i => i.PurchaseInvoiceDetails)
                .FirstOrDefaultAsync(r => r.ReceiptId == receiptId);

            if (receipt == null)
            {
                ErrorMessage = "Không tìm thấy phiếu mua hàng con.";
                return Back();
            }

            if (receipt.Status == "Received")
            {
                ErrorMessage = $"Phiếu {receipt.ReceiptCode} đã được nhập kho hoàn tất.";
                return Back();
            }

            if (receipt.Status == "WaitManagerApproval")
            {
                ErrorMessage = $"Phiếu {receipt.ReceiptCode} đã tạo phiếu nhập kho và đang chờ Manager phê duyệt.";
                return Back();
            }

            var materialId = receipt.Invoice?.PurchaseInvoiceDetails.FirstOrDefault()?.MaterialId;
            RawMaterial? material = null;
            if (materialId.HasValue)
            {
                material = await _context.RawMaterials.FirstOrDefaultAsync(m => m.MaterialId == materialId.Value);
            }
            material ??= await _context.RawMaterials.FirstOrDefaultAsync();
            if (material == null)
            {
                ErrorMessage = "Hệ thống chưa có danh mục nguyên liệu thô.";
                return Back();
            }

            // Sinh mã Phiếu nhập kho nguyên liệu: PNK-26xxx
            var prefix = $"PNK-{DateTime.Now.Year % 100:D2}";
            var existingCodes = await _context.RawMaterialImports
                .AsNoTracking()
                .Where(i => i.ImportCode != null && i.ImportCode.StartsWith(prefix))
                .Select(i => i.ImportCode!)
                .ToListAsync();
            var nextSeq = existingCodes
                .Select(c => int.TryParse(c.Substring(prefix.Length), out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;
            var importCode = $"{prefix}{nextSeq:D3}";

            var importTicket = new RawMaterialImport
            {
                ImportCode = importCode,
                ReceiptId = receipt.ReceiptId,
                MaterialId = material.MaterialId,
                SupplierId = receipt.SupplierId,
                Quantity = receipt.WeighedQuantity,
                UnitPrice = receipt.ActualPrice,
                ImportDate = DateTime.Now,
                ImportedBy = CurrentUserId,
                Status = "Pending",
                Note = $"Phiếu nhập kho nguyên liệu {importCode} tạo từ phiếu mua hàng con {receipt.ReceiptCode} — Chờ Manager phê duyệt"
            };

            _context.RawMaterialImports.Add(importTicket);
            receipt.Status = "WaitManagerApproval";

            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã tạo Phiếu nhập kho nguyên liệu {importCode} (từ phiếu con {receipt.ReceiptCode}) và gửi cho Manager phê duyệt.";
            return Back();
        }

        // BƯỚC 2: Manager (hoặc Owner) phê duyệt Phiếu nhập kho nguyên liệu -> Cộng tồn kho và chuyển phiếu con sang Đã nhập kho
        public async Task<IActionResult> OnPostApproveImportAsync(int? importId, int? receiptId)
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            IActionResult Back() => RedirectToPage(new
            {
                Tab = "raw",
                SearchTerm,
                StatusFilter,
                SupplierFilter,
                DateFilter,
                PageSize,
                CurrentPage
            });

            RawMaterialImport? importTicket = null;
            if (importId.HasValue && importId.Value > 0)
            {
                importTicket = await _context.RawMaterialImports
                    .Include(i => i.Receipt)
                    .FirstOrDefaultAsync(i => i.ImportId == importId.Value);
            }
            else if (receiptId.HasValue && receiptId.Value > 0)
            {
                importTicket = await _context.RawMaterialImports
                    .Include(i => i.Receipt)
                    .OrderByDescending(i => i.ImportId)
                    .FirstOrDefaultAsync(i => i.ReceiptId == receiptId.Value);
            }

            if (importTicket == null)
            {
                ErrorMessage = "Không tìm thấy phiếu nhập kho nguyên liệu cần phê duyệt.";
                return Back();
            }

            if (importTicket.Status == "Approved")
            {
                ErrorMessage = $"Phiếu nhập kho {importTicket.ImportCode} đã được phê duyệt trước đó.";
                return Back();
            }

            importTicket.Status = "Approved";
            importTicket.ApprovedBy = CurrentUserId;
            importTicket.ApprovedAt = DateTime.Now;
            importTicket.Note = $"Đã được phê duyệt nhập kho bởi {CurrentFullName} lúc {DateTime.Now:HH:mm dd/MM/yyyy}";

            if (importTicket.Receipt != null)
            {
                importTicket.Receipt.Status = "Received";
            }
            else if (importTicket.ReceiptId.HasValue)
            {
                var r = await _context.PurchaseReceipts.FindAsync(importTicket.ReceiptId.Value);
                if (r != null) r.Status = "Received";
            }

            var material = await _context.RawMaterials.FirstOrDefaultAsync(m => m.MaterialId == importTicket.MaterialId)
                           ?? await _context.RawMaterials.FirstOrDefaultAsync();
            if (material != null)
            {
                material.CurrentQuantity += importTicket.Quantity;
            }

            await _context.SaveChangesAsync();

            var codeDisplay = string.IsNullOrWhiteSpace(importTicket.ImportCode) ? $"PNK-26{importTicket.ImportId:D3}" : importTicket.ImportCode;
            SuccessMessage = $"Đã phê duyệt Phiếu nhập kho nguyên liệu {codeDisplay}: Đã cộng +{importTicket.Quantity:N0} kg vào tồn kho nguyên liệu.";
            return Back();
        }

        // Manager / Owner từ chối Phiếu nhập kho nguyên liệu
        public async Task<IActionResult> OnPostRejectImportAsync(int? importId, int? receiptId)
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            IActionResult Back() => RedirectToPage(new
            {
                Tab = "raw",
                SearchTerm,
                StatusFilter,
                SupplierFilter,
                DateFilter,
                PageSize,
                CurrentPage
            });

            RawMaterialImport? importTicket = null;
            if (importId.HasValue && importId.Value > 0)
            {
                importTicket = await _context.RawMaterialImports
                    .Include(i => i.Receipt)
                    .FirstOrDefaultAsync(i => i.ImportId == importId.Value);
            }
            else if (receiptId.HasValue && receiptId.Value > 0)
            {
                importTicket = await _context.RawMaterialImports
                    .Include(i => i.Receipt)
                    .OrderByDescending(i => i.ImportId)
                    .FirstOrDefaultAsync(i => i.ReceiptId == receiptId.Value);
            }

            if (importTicket == null)
            {
                ErrorMessage = "Không tìm thấy phiếu nhập kho nguyên liệu.";
                return Back();
            }

            if (importTicket.Status == "Approved")
            {
                ErrorMessage = "Không thể từ chối phiếu nhập kho đã được duyệt.";
                return Back();
            }

            importTicket.Status = "Rejected";
            importTicket.ApprovedBy = CurrentUserId;
            importTicket.ApprovedAt = DateTime.Now;

            if (importTicket.Receipt != null)
            {
                importTicket.Receipt.Status = "Rejected";
            }

            await _context.SaveChangesAsync();

            var codeDisplay = string.IsNullOrWhiteSpace(importTicket.ImportCode) ? $"PNK-26{importTicket.ImportId:D3}" : importTicket.ImportCode;
            SuccessMessage = $"Đã từ chối Phiếu nhập kho nguyên liệu {codeDisplay}.";
            return Back();
        }
    }
}
