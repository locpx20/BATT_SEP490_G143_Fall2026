using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BATT_PPMS.Pages.Inventory
{
    public class WarehouseReceiptRowViewModel
    {
        public int ReceiptId { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public int InvoiceId { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = "Nguyên liệu thô";
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal WeighedQuantity { get; set; }
        public decimal ActualPrice { get; set; }
        public decimal? ImportedQuantity { get; set; }
        public decimal? ImportedUnitPrice { get; set; }
        public decimal DisplayQuantity => ImportedQuantity ?? WeighedQuantity;
        public decimal DisplayPrice => ImportedUnitPrice ?? ActualPrice;
        public decimal TotalAmount => DisplayQuantity * DisplayPrice;
        public string Status { get; set; } = "WaitWarehouse";
        public string StatusText { get; set; } = "Đã gửi yêu cầu nhập kho — Chờ Thủ kho xử lý";
        public string StatusType { get; set; } = "warehouse"; // pending | warehouse | received | rejected
        public string? RejectionReason { get; set; }
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
        public int SupplierId { get; set; }
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
        public string? Note { get; set; }
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
        public string ViewMode { get; set; } = "requests"; // requests | imports

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateOnly? ProductionDateFilter { get; set; }

        public string? ProductionDateInput => ProductionDateFilter?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public bool InvalidProductionDateFilter =>
            ModelState.TryGetValue(nameof(ProductionDateFilter), out var state) && state.Errors.Count > 0;

        [BindProperty(SupportsGet = true)]
        public string? StockGrade { get; set; }

        public FinishedStockTotal TotalFinishedStock { get; private set; } = new(0, 0);
        public FinishedStockTotal StockA { get; private set; } = new(0, 0);
        public FinishedStockTotal StockB { get; private set; } = new(0, 0);
        public bool CanViewFinishedQcImage => IsWarehouseStaff || IsManager || IsOwner;

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
        public List<FinishedStockLot> Batches { get; private set; } = [];
        public PageWindow Pagination { get; set; } = new(0, 1);

        public List<WarehouseReceiptRowViewModel> Receipts { get; set; } = new();
        public List<WarehouseReceiptRowViewModel> PendingInboundRequests { get; set; } = new();
        public List<RawMaterialImportRowViewModel> ImportTickets { get; set; } = new();
        public int TotalRequestsCount { get; set; }
        public int TotalImportsCount { get; set; }
        public List<SupplierFilterOption> Suppliers { get; set; } = new();
        public string NextSuggestedImportCode { get; set; } = string.Empty;

        // KPI
        public decimal TotalRawInventoryKg { get; set; }
        public int PendingWarehouseCount { get; set; }
        public int WaitingManagerApprovalCount { get; set; }
        public int ReceivedReceiptsCount { get; set; }
        public decimal TotalWeighedKg { get; set; }

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

            // Keep previously issued consumption-notification links working after the split.
            if (IsWarehouseStaff && Request.Query.ContainsKey("UsageStatusFilter"))
                return RedirectToPage("/Warehouse/Consumption", new { StatusFilter = Request.Query["UsageStatusFilter"].ToString(), pageNumber = Request.Query["usagePageNumber"].ToString() });
            if (Tab != "finished") Tab = "raw";

            if (Tab == "finished")
            {
                await LoadFinishedStockAsync(HttpContext.RequestAborted);
                if (IsWarehouseStaff)
                    await WarehouseNavigationNotices.OpenAsync(_context, CurrentUserId, WarehouseNavigationNotice.FinishedStock, HttpContext.RequestAborted);
                return Page();
            }

            if (PageSize <= 0) PageSize = 10;
            if (CurrentPage <= 0) CurrentPage = 1;

            Materials = await _context.RawMaterials.AsNoTracking()
                .OrderBy(x => x.MaterialName)
                .ThenBy(x => x.MaterialId)
                .ToListAsync();

            await LoadDataAsync();
            return Page();
        }

        private async Task LoadFinishedStockAsync(CancellationToken cancellationToken)
        {
            StockGrade = StockGrade is "A" or "B" ? StockGrade : null;
            var gradedLots = from request in _context.ProductionQualityRequests.AsNoTracking()
                             join result in _context.ProductionQcResults on request.RequestId equals result.RequestId
                             where request.Inspection.Status == "Completed" && request.Inspection.Result == "Pass"
                             select new { request.Inspection.BatchId, request.RequestId, result.Grade };
            var latestResults = gradedLots.GroupBy(x => x.BatchId).Select(group => group.Max(x => x.RequestId));
            var latestGrades = gradedLots.Where(x => latestResults.Contains(x.RequestId));
            var stock = from batch in _context.FinishedGoodBatches.AsNoTracking()
                        where batch.Status == "In Stock"
                        join grade in latestGrades on batch.BatchId equals grade.BatchId into grades
                        from grade in grades.DefaultIfEmpty()
                        select new FinishedStockLot
                        {
                            BatchId = batch.BatchId,
                            Lot = batch.BagNumber,
                            ProductionDate = batch.ProductionDate,
                            StockKg = batch.Quantity,
                            Grade = grade.Grade
                        };

            var totals = await stock.Where(x => x.StockKg > 0).GroupBy(x => x.Grade)
                .Select(group => new { Grade = group.Key, Kg = group.Sum(x => x.StockKg), Lots = group.Count() })
                .ToListAsync(cancellationToken);
            TotalFinishedStock = new(totals.Sum(x => x.Kg), totals.Sum(x => x.Lots));
            var gradeA = totals.Find(x => x.Grade == "A");
            var gradeB = totals.Find(x => x.Grade == "B");
            StockA = new(gradeA?.Kg ?? 0, gradeA?.Lots ?? 0);
            StockB = new(gradeB?.Kg ?? 0, gradeB?.Lots ?? 0);

            var query = stock;
            if (StockGrade != null)
                query = query.Where(x => x.Grade == StockGrade);
            if (InvalidProductionDateFilter)
                query = query.Where(x => false);
            else if (ProductionDateFilter is { } productionDate)
                query = query.Where(x => x.ProductionDate == productionDate);

            Pagination = new(await query.CountAsync(cancellationToken), PageWindow.ReadRequestedPage(Request.Query, PageNumber));
            Batches = await query.OrderByDescending(x => x.ProductionDate).ThenByDescending(x => x.BatchId)
                .Skip(Pagination.Offset).Take(Pagination.PageSize).ToListAsync(cancellationToken);
            if (Batches.Count == 0)
                return;

            var batchIds = Batches.Select(x => x.BatchId).ToArray();
            var receipts = await _context.FinishedGoodsReceipts.AsNoTracking()
                .Where(r => batchIds.Contains(r.QualityRequest.Inspection.BatchId)
                    && r.ReceivedAt != null && r.ActualQuantity > 0
                    && (r.Status == "Received" || r.Status == "AwaitingStaffConfirmation"))
                .OrderByDescending(r => r.ReceivedAt).ThenByDescending(r => r.ReceiptId)
                .Select(r => new
                {
                    r.QualityRequest.Inspection.BatchId,
                    Receipt = new FinishedStockReceipt(r.ReceiptId, r.QualityRequestId, r.ReceivedAt,
                        r.ActualQuantity, r.Receiver == null ? null : r.Receiver.FullName, r.DiscrepancyNote,
                        r.QualityRequest.ResultImage != null)
                }).ToListAsync(cancellationToken);
            var latestReceipts = receipts.GroupBy(x => x.BatchId).ToDictionary(group => group.Key, group => group.First().Receipt);
            foreach (var batch in Batches)
                batch.Receipt = latestReceipts.GetValueOrDefault(batch.BatchId);
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

            var mappedImports = allImports.Select(i =>
            {
                var st = (i.Status ?? "Pending").Trim();
                var (stText, stType) = st switch
                {
                    "Approved" => ("Đã duyệt", "received"),
                    "Rejected" => ("Từ chối PNK", "rejected"),
                    _ => ("Chờ duyệt", "warehouse")
                };

                var invCode = i.Receipt?.Invoice != null
                    ? BATT_PPMS.Pages.PurchaseInvoices.IndexModel.FormatInvoiceBadgeCode(i.Receipt.Invoice.InvoiceId > 0 ? i.Receipt.Invoice.InvoiceCode : null, i.Receipt.Invoice.InvoiceId)
                    : "—";

                return new RawMaterialImportRowViewModel
                {
                    ImportId = i.ImportId,
                    ImportCode = string.IsNullOrWhiteSpace(i.ImportCode) ? $"PNK-26{i.ImportId:D3}" : i.ImportCode!,
                    ReceiptId = i.ReceiptId,
                    ReceiptCode = i.Receipt?.ReceiptCode ?? "—",
                    InvoiceCode = invCode,
                    MaterialName = i.Material?.MaterialName ?? "Nguyên liệu thô",
                    SupplierId = i.SupplierId,
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
                    Note = i.Note,
                    ScalePhotoPath = i.Receipt?.ScalePhotoPath,
                    InvoicePhotoPath = i.Receipt?.InvoicePhotoPath
                };
            }).ToList();

            TotalImportsCount = mappedImports.Count;

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

            // Thủ kho chỉ làm việc với các phiếu đã gửi Yêu cầu nhập kho (Inbound Request: WaitWarehouse, Pending, ImportRejected, WaitManagerApproval, Received)
            var warehouseReceipts = IsWarehouseStaff
                ? allReceipts.Where(r => r.Status != "PendingOrderApproval" && r.Status != "OrderApproved" && r.Status != "OrderRejected").ToList()
                : allReceipts;

            PendingWarehouseCount = warehouseReceipts.Count(r => r.Status == null || r.Status == "Pending" || r.Status == "WaitWarehouse" || r.Status == "ImportRejected");
            WaitingManagerApprovalCount = warehouseReceipts.Count(r => r.Status == "WaitManagerApproval") +
                                          allImports.Count(i => i.Status == "Pending" && !i.ReceiptId.HasValue);
            ReceivedReceiptsCount = warehouseReceipts.Count(r => r.Status == "Received");
            TotalWeighedKg = warehouseReceipts.Sum(r => r.WeighedQuantity);

            var mapped = warehouseReceipts.Select(r =>
            {
                var (statusText, statusType) = BATT_PPMS.Pages.PurchaseInvoices.DetailModel.ReceiptStatus(r.Status);
                var invoiceCode = r.Invoice != null
                    ? BATT_PPMS.Pages.PurchaseInvoices.IndexModel.FormatInvoiceBadgeCode(r.Invoice.InvoiceCode, r.Invoice.InvoiceId)
                    : $"TM-26{r.InvoiceId:D3}";

                var defaultMatId = Materials.FirstOrDefault()?.MaterialId ?? 1;
                var matDetail = r.Invoice?.PurchaseInvoiceDetails.FirstOrDefault(d => d.Material != null)
                                ?? r.Invoice?.PurchaseInvoiceDetails.FirstOrDefault();
                var matId = matDetail?.MaterialId ?? defaultMatId;
                var matName = matDetail?.Material?.MaterialName ?? "Nguyên liệu thô";

                importByReceiptId.TryGetValue(r.ReceiptId, out var linkedImport);

                return new WarehouseReceiptRowViewModel
                {
                    ReceiptId = r.ReceiptId,
                    ReceiptCode = r.ReceiptCode,
                    InvoiceId = r.InvoiceId,
                    InvoiceCode = invoiceCode,
                    MaterialId = matId,
                    MaterialName = matName,
                    SupplierId = r.SupplierId,
                    SupplierName = r.Supplier?.SupplierName ?? "—",
                    WeighedQuantity = r.WeighedQuantity,
                    ActualPrice = r.ActualPrice,
                    ImportedQuantity = linkedImport?.Quantity,
                    ImportedUnitPrice = linkedImport?.UnitPrice,
                    Status = r.Status ?? "WaitWarehouse",
                    StatusText = statusText,
                    StatusType = statusType,
                    RejectionReason = r.RejectionReason,
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
            }).ToList();

            TotalRequestsCount = mapped.Count;
            TotalWeighedKg = mapped.Sum(r => r.DisplayQuantity);

            var prefix = $"PNK-{DateTime.Now.Year % 100:D2}";
            var nextSeq = allImports
                .Where(i => !string.IsNullOrWhiteSpace(i.ImportCode) && i.ImportCode.StartsWith(prefix))
                .Select(i => int.TryParse(i.ImportCode!.Substring(prefix.Length), out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;
            NextSuggestedImportCode = $"{prefix}{nextSeq:D3}";

            PendingInboundRequests = mapped
                .Where(r => r.Status == "WaitWarehouse" || r.Status == "Pending" || r.Status == "ImportRejected")
                .ToList();

            if (!string.Equals(ViewMode, "imports", StringComparison.OrdinalIgnoreCase))
            {
                ViewMode = "requests";
            }
            else
            {
                ViewMode = "imports";
            }

            var now = DateTime.Now;
            if (ViewMode == "imports")
            {
                var filteredImports = mappedImports.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(DateFilter) && DateFilter != "all")
                {
                    if (DateFilter == "today")
                    {
                        var today = now.Date;
                        filteredImports = filteredImports.Where(i => i.ImportDate.Date == today);
                    }
                    else if (DateFilter == "week")
                    {
                        var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday);
                        filteredImports = filteredImports.Where(i => i.ImportDate >= startOfWeek);
                    }
                    else if (DateFilter == "month")
                    {
                        var startOfMonth = new DateTime(now.Year, now.Month, 1);
                        filteredImports = filteredImports.Where(i => i.ImportDate >= startOfMonth);
                    }
                }

                if (SupplierFilter.HasValue && SupplierFilter.Value > 0)
                {
                    filteredImports = filteredImports.Where(i => i.SupplierId == SupplierFilter.Value);
                }

                if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "all")
                {
                    var sf = StatusFilter.Trim().ToLowerInvariant();
                    if (sf == "pending" || sf == "wait_manager")
                    {
                        filteredImports = filteredImports.Where(i => i.Status == "Pending");
                    }
                    else if (sf == "received" || sf == "approved")
                    {
                        filteredImports = filteredImports.Where(i => i.Status == "Approved");
                    }
                    else if (sf == "rejected")
                    {
                        filteredImports = filteredImports.Where(i => i.Status == "Rejected");
                    }
                }

                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    var term = SearchTerm.Trim().ToLowerInvariant();
                    filteredImports = filteredImports.Where(i =>
                        i.ImportCode.ToLowerInvariant().Contains(term) ||
                        i.ReceiptCode.ToLowerInvariant().Contains(term) ||
                        i.InvoiceCode.ToLowerInvariant().Contains(term) ||
                        i.SupplierName.ToLowerInvariant().Contains(term) ||
                        i.ImportedByName.ToLowerInvariant().Contains(term) ||
                        i.MaterialName.ToLowerInvariant().Contains(term));
                }

                var finalImports = filteredImports.ToList();
                TotalItems = finalImports.Count;
                if (CurrentPage > TotalPages) CurrentPage = TotalPages;
                if (CurrentPage < 1) CurrentPage = 1;

                ImportTickets = finalImports
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();
                return;
            }

            ImportTickets = mappedImports;
            var filtered = mapped.AsEnumerable();

            // Lọc theo ngày
            if (!string.IsNullOrWhiteSpace(DateFilter) && DateFilter != "all")
            {
                if (DateFilter == "today")
                {
                    var today = now.Date;
                    filtered = filtered.Where(r => r.CreatedAt.Date == today);
                }
                else if (DateFilter == "week")
                {
                    var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday);
                    filtered = filtered.Where(r => r.CreatedAt >= startOfWeek);
                }
                else if (DateFilter == "month")
                {
                    var startOfMonth = new DateTime(now.Year, now.Month, 1);
                    filtered = filtered.Where(r => r.CreatedAt >= startOfMonth);
                }
            }

            // Lọc theo nhà cung cấp
            if (SupplierFilter.HasValue && SupplierFilter.Value > 0)
            {
                filtered = filtered.Where(r => r.SupplierId == SupplierFilter.Value);
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "all")
            {
                var sf = StatusFilter.Trim().ToLowerInvariant();
                if (sf == "pending")
                {
                    filtered = filtered.Where(r => r.Status == "Pending" || r.Status == "WaitWarehouse" || r.Status == "ImportRejected");
                }
                else if (sf == "wait_manager")
                {
                    filtered = filtered.Where(r => r.Status == "WaitManagerApproval");
                }
                else if (sf == "received")
                {
                    filtered = filtered.Where(r => r.Status == "Received");
                }
            }

            // Lọc theo từ khóa
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                filtered = filtered.Where(r =>
                    r.ReceiptCode.ToLowerInvariant().Contains(term) ||
                    r.InvoiceCode.ToLowerInvariant().Contains(term) ||
                    (r.ImportCode != null && r.ImportCode.ToLowerInvariant().Contains(term)) ||
                    r.SupplierName.ToLowerInvariant().Contains(term) ||
                    r.CreatedByName.ToLowerInvariant().Contains(term) ||
                    r.MaterialName.ToLowerInvariant().Contains(term));
            }

            var finalList = filtered.ToList();
            TotalItems = finalList.Count;
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            Receipts = finalList
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }

        private static bool TryParsePositiveDecimal(string? raw, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var cleaned = raw.Trim().Replace(" ", "");
            if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value) && value > 0)
                return true;
            if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("vi-VN"), out value) && value > 0)
                return true;
            return false;
        }

        // BƯỚC 5 TRÊN SWIMLANE: Warehouse Manager -> Check and compare with IR -> Create Goods Receipt Raw Material note (PNK-26xxx)
        public async Task<IActionResult> OnPostConfirmReceiptAsync(
            int receiptId,
            int? materialId,
            int? supplierId,
            string? importedQuantity,
            string? unitPrice,
            string? warehouseNote)
        {
            var check = RequireRole(RoleConstants.WarehouseStaff, RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            IActionResult Back(string? targetView = null) => RedirectToPage(new
            {
                Tab = "raw",
                ViewMode = targetView ?? ViewMode,
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
                ErrorMessage = "Không tìm thấy phiếu yêu cầu nhập kho.";
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

            var targetMaterialId = (materialId.HasValue && materialId.Value > 0)
                ? materialId.Value
                : receipt.Invoice?.PurchaseInvoiceDetails.FirstOrDefault()?.MaterialId;
            RawMaterial? material = null;
            if (targetMaterialId.HasValue)
            {
                material = await _context.RawMaterials.FirstOrDefaultAsync(m => m.MaterialId == targetMaterialId.Value);
            }
            material ??= await _context.RawMaterials.FirstOrDefaultAsync();
            if (material == null)
            {
                ErrorMessage = "Hệ thống chưa có danh mục nguyên liệu thô.";
                return Back();
            }

            var targetSupplierId = (supplierId.HasValue && supplierId.Value > 0) ? supplierId.Value : receipt.SupplierId;

            decimal finalQuantity = receipt.WeighedQuantity;
            if (!string.IsNullOrWhiteSpace(importedQuantity))
            {
                if (!TryParsePositiveDecimal(importedQuantity, out finalQuantity))
                {
                    ErrorMessage = "Khối lượng nhập kho phải lớn hơn 0.";
                    return Back();
                }
            }

            decimal finalUnitPrice = receipt.ActualPrice;
            if (!string.IsNullOrWhiteSpace(unitPrice))
            {
                if (!TryParsePositiveDecimal(unitPrice, out finalUnitPrice))
                {
                    ErrorMessage = "Đơn giá nhập kho phải lớn hơn 0.";
                    return Back();
                }
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

            var noteText = string.IsNullOrWhiteSpace(warehouseNote)
                ? $"Phiếu nhập kho nguyên liệu {importCode} tạo từ {receipt.ReceiptCode} — Chờ Manager phê duyệt"
                : $"Phiếu nhập kho {importCode} (từ {receipt.ReceiptCode}): {warehouseNote.Trim()}";

            var estimatedQuantity = receipt.WeighedQuantity;
            if (finalQuantity != estimatedQuantity)
            {
                noteText += $" (KL ước tính: {estimatedQuantity:N0} kg → KL cân thực tế: {finalQuantity:N0} kg)";
            }

            var importTicket = new RawMaterialImport
            {
                ImportCode = importCode,
                ReceiptId = receipt.ReceiptId,
                MaterialId = material.MaterialId,
                SupplierId = targetSupplierId,
                Quantity = finalQuantity,
                UnitPrice = finalUnitPrice,
                ImportDate = DateTime.Now,
                ImportedBy = CurrentUserId,
                Status = "Pending",
                Note = noteText
            };

            _context.RawMaterialImports.Add(importTicket);
            // Cập nhật khối lượng cân thực tế tại kho và đơn giá thực nhập vào phiếu con để đồng bộ toàn hệ thống
            receipt.WeighedQuantity = finalQuantity;
            receipt.ActualPrice = finalUnitPrice;
            receipt.Status = "WaitManagerApproval";
            receipt.RejectionReason = null;

            // Gửi thông báo cho Manager duyệt Phiếu nhập kho nguyên liệu
            var managerIds = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Status == StatusConstants.Active &&
                            (u.Role.RoleName == RoleConstants.Manager || u.Role.RoleName == RoleConstants.Owner))
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var mgrId in managerIds)
            {
                Notifier.Add(
                    _context,
                    mgrId,
                    $"Phiếu nhập kho nguyên liệu {importCode} chờ duyệt",
                    $"Thủ kho ({CurrentFullName}) đã đối chiếu IR và lập Phiếu nhập kho nguyên liệu {importCode} (từ phiếu {receipt.ReceiptCode}, KL cân thực tế {finalQuantity:N0} kg × {finalUnitPrice:N0} VNĐ/kg). Chờ Manager phê duyệt.",
                    "/Inventory/Index?tab=raw");
            }

            await _context.SaveChangesAsync();
            await BATT_PPMS.Pages.PurchaseInvoices.IndexModel.ExpirePastDayInvoicesAsync(_context);

            SuccessMessage = $"Đã lập Phiếu nhập kho nguyên liệu {importCode} (KL cân thực tế: {finalQuantity:N0} kg × {finalUnitPrice:N0} VNĐ/kg) từ phiếu {receipt.ReceiptCode} và gửi cho Manager phê duyệt.";
            return Back("imports");
        }

        // BƯỚC 6A TRÊN SWIMLANE: Manager -> Approved (Yes) -> Cộng tồn kho & gửi Receive Goods Receipt information về cho Purchasing Staff
        public async Task<IActionResult> OnPostApproveImportAsync(int? importId, int? receiptId)
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            IActionResult Back(string? targetView = null) => RedirectToPage(new
            {
                Tab = "raw",
                ViewMode = targetView ?? ViewMode,
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
                        .ThenInclude(r => r!.Invoice)
                    .FirstOrDefaultAsync(i => i.ImportId == importId.Value);
            }
            else if (receiptId.HasValue && receiptId.Value > 0)
            {
                importTicket = await _context.RawMaterialImports
                    .Include(i => i.Receipt)
                        .ThenInclude(r => r!.Invoice)
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

            var codeDisplay = string.IsNullOrWhiteSpace(importTicket.ImportCode) ? $"PNK-26{importTicket.ImportId:D3}" : importTicket.ImportCode;

            importTicket.Status = "Approved";
            importTicket.ApprovedBy = CurrentUserId;
            importTicket.ApprovedAt = DateTime.Now;
            importTicket.Note = $"Đã được phê duyệt nhập kho bởi {CurrentFullName} lúc {DateTime.Now:HH:mm dd/MM/yyyy}";

            PurchaseReceipt? linkedReceipt = importTicket.Receipt;
            if (linkedReceipt == null && importTicket.ReceiptId.HasValue)
            {
                linkedReceipt = await _context.PurchaseReceipts
                    .Include(r => r.Invoice)
                    .FirstOrDefaultAsync(r => r.ReceiptId == importTicket.ReceiptId.Value);
            }

            if (linkedReceipt != null)
            {
                linkedReceipt.Status = "Received";
                linkedReceipt.RejectionReason = null;

                // Gửi thông tin Phiếu nhập kho (Receive Goods Receipt information) về cho Purchasing Staff
                var notifyStaffIds = new HashSet<int>();
                if (linkedReceipt.CreatedBy.HasValue) notifyStaffIds.Add(linkedReceipt.CreatedBy.Value);
                if (linkedReceipt.Invoice?.AssignedTo != null) notifyStaffIds.Add(linkedReceipt.Invoice.AssignedTo.Value);

                foreach (var staffId in notifyStaffIds)
                {
                    Notifier.Add(
                        _context,
                        staffId,
                        $"Hoàn tất nhập kho nguyên liệu ({codeDisplay})",
                        $"Manager ({CurrentFullName}) đã duyệt Phiếu nhập kho {codeDisplay} (từ đơn mua {linkedReceipt.ReceiptCode}, khối lượng +{importTicket.Quantity:N0} kg). Thông tin phiếu nhập kho đã được cập nhật.",
                        $"/PurchaseInvoices/Detail?id={linkedReceipt.InvoiceId}");
                }
            }

            var material = await _context.RawMaterials.FirstOrDefaultAsync(m => m.MaterialId == importTicket.MaterialId)
                           ?? await _context.RawMaterials.FirstOrDefaultAsync();
            if (material != null)
            {
                material.CurrentQuantity += importTicket.Quantity;
            }

            await _context.SaveChangesAsync();
            await BATT_PPMS.Pages.PurchaseInvoices.IndexModel.ExpirePastDayInvoicesAsync(_context);

            SuccessMessage = $"Đã phê duyệt Phiếu nhập kho nguyên liệu {codeDisplay}: Đã cộng +{importTicket.Quantity:N0} kg vào tồn kho và gửi thông tin phiếu nhập kho về cho Nhân viên thu mua.";
            return Back();
        }

        // BƯỚC 6B TRÊN SWIMLANE: Manager -> Approved (No) -> Rejection Reason -> quay lại Warehouse Manager: Create Goods Receipt Raw Material note
        public async Task<IActionResult> OnPostRejectImportAsync(int? importId, int? receiptId, string? rejectionReason)
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            IActionResult Back(string? targetView = null) => RedirectToPage(new
            {
                Tab = "raw",
                ViewMode = targetView ?? ViewMode,
                SearchTerm,
                StatusFilter,
                SupplierFilter,
                DateFilter,
                PageSize,
                CurrentPage
            });

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                ErrorMessage = "Vui lòng nhập lý do từ chối (Rejection Reason) cho phiếu nhập kho nguyên liệu.";
                return Back();
            }

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

            var reason = rejectionReason.Trim();
            var codeDisplay = string.IsNullOrWhiteSpace(importTicket.ImportCode) ? $"PNK-26{importTicket.ImportId:D3}" : importTicket.ImportCode;

            importTicket.Status = "Rejected";
            importTicket.ApprovedBy = CurrentUserId;
            importTicket.ApprovedAt = DateTime.Now;
            importTicket.Note = $"[Lý do từ chối]: {reason}";

            if (importTicket.Receipt != null)
            {
                // Chuyển về ImportRejected để Thủ kho thấy Lý do từ chối và bấm "Lập lại phiếu nhập kho (Create Goods Receipt Raw Material note)"
                importTicket.Receipt.Status = "ImportRejected";
                importTicket.Receipt.RejectionReason = reason;
            }

            if (importTicket.ImportedBy.HasValue)
            {
                Notifier.Add(
                    _context,
                    importTicket.ImportedBy.Value,
                    $"Phiếu nhập kho {codeDisplay} bị từ chối",
                    $"Manager ({CurrentFullName}) đã từ chối phiếu nhập kho {codeDisplay}. Lý do: {reason}. Vui lòng kiểm tra và lập lại phiếu nhập kho nguyên liệu.",
                    "/Inventory/Index?tab=raw");
            }

            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã từ chối Phiếu nhập kho nguyên liệu {codeDisplay} kèm lý do và gửi trả về cho Thủ kho lập lại phiếu nhập kho.";
            return Back();
        }
    }
}
