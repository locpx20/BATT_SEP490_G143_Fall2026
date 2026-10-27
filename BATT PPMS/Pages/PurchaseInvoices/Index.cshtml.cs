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
        public int PlannedQuantity { get; set; }
        public int WeighedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public int CreatedByStaffId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public int? ApprovedByStaffId { get; set; }
        public string? ApprovedByName { get; set; }
        public string Status { get; set; } = "Pending";
        public string? Note { get; set; }
        public string? RejectionReason { get; set; }
    }

    public class SupplierPurchaseReportItemViewModel
    {
        public int SupplierId { get; set; }
        public string SupplierCode => $"NCC-{SupplierId:D2}";
        public string SupplierName { get; set; } = string.Empty;
        public int TripCount { get; set; }
        public decimal TotalWeightKg { get; set; }
        public decimal PercentageOfTotal { get; set; }
        public decimal AverageUnitPrice { get; set; }
        public decimal TotalAmountVnd { get; set; }
        public string Status { get; set; } = "Active";
    }

    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "orders";

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ToDate { get; set; }

        public List<PurchaseOrderItemViewModel> Orders { get; set; } = new();
        public List<SupplierPurchaseReportItemViewModel> Reports { get; set; } = new();

        // Overall KPIs
        public decimal TotalSpendVnd { get; set; }
        public decimal TotalWeighedKg { get; set; }
        public int PendingOrdersCount { get; set; }
        public decimal AverageUnitPriceVnd { get; set; }

        public bool CanApprove => IsOwner;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager, RoleConstants.PurchasingStaff);
            if (check != null) return check;

            if (string.IsNullOrWhiteSpace(Tab))
                Tab = "orders";
            Tab = Tab.ToLowerInvariant();

            await LoadDataAsync();
            return Page();
        }

        // Schema mới: PurchaseInvoice không còn RawMaterialType / PlannedQuantity / WeighedQuantity / UnitPrice.
        // Các giá trị này được suy ra từ PurchaseInvoiceDetail (+ RawMaterial).
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

        private async Task LoadDataAsync()
        {
            var query = _context.PurchaseInvoices
                .Include(p => p.Supplier)
                .Include(p => p.CreatedByNavigation)
                .Include(p => p.ApprovedByNavigation)
                .Include(p => p.PurchaseInvoiceDetails)
                    .ThenInclude(d => d.Material)
                .AsNoTracking()
                .AsQueryable();

            var allInvoices = await query.ToListAsync();

            // Overall KPIs
            TotalSpendVnd = allInvoices.Sum(p => p.TotalAmount);
            TotalWeighedKg = allInvoices.Sum(QuantityOf);
            PendingOrdersCount = allInvoices.Count(p => string.Equals(p.Status, "Pending", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Status, "Submitted", StringComparison.OrdinalIgnoreCase));
            AverageUnitPriceVnd = TotalWeighedKg > 0 ? Math.Round(TotalSpendVnd / TotalWeighedKg, 0) : 0;

            // Date filtering for reports or general list
            DateTime? parsedFrom = null;
            DateTime? parsedTo = null;
            if (DateTime.TryParse(FromDate, out var fDate)) parsedFrom = fDate.Date;
            if (DateTime.TryParse(ToDate, out var tDate)) parsedTo = tDate.Date.AddDays(1).AddTicks(-1);

            var filtered = allInvoices.AsEnumerable();

            if (parsedFrom.HasValue)
                filtered = filtered.Where(p => p.PurchaseDate >= parsedFrom.Value);
            if (parsedTo.HasValue)
                filtered = filtered.Where(p => p.PurchaseDate <= parsedTo.Value);

            // Tab 1: Orders
            var orderFiltered = filtered;
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                orderFiltered = orderFiltered.Where(p =>
                    p.InvoiceCode.ToLowerInvariant().Contains(term) ||
                    (p.Supplier?.SupplierName != null && p.Supplier.SupplierName.ToLowerInvariant().Contains(term)) ||
                    MaterialTypeOf(p).ToLowerInvariant().Contains(term) ||
                    (p.CreatedByNavigation?.FullName != null && p.CreatedByNavigation.FullName.ToLowerInvariant().Contains(term)) ||
                    (NoteTags.Plain(p.Note) != null && NoteTags.Plain(p.Note)!.ToLowerInvariant().Contains(term))
                );
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "ALL")
            {
                orderFiltered = orderFiltered.Where(p => string.Equals(p.Status, StatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            Orders = orderFiltered
                .OrderByDescending(p => p.PurchaseDate)
                .ThenByDescending(p => p.InvoiceId)
                .Select(p =>
                {
                    var qty = (int)Math.Round(QuantityOf(p));
                    return new PurchaseOrderItemViewModel
                    {
                        InvoiceId = p.InvoiceId,
                        InvoiceCode = p.InvoiceCode,
                        SupplierId = p.SupplierId,
                        SupplierName = p.Supplier?.SupplierName ?? "Nhà cung cấp chưa đặt tên",
                        SupplierPhone = p.Supplier?.Phone,
                        SupplierAddress = p.Supplier?.Address,
                        RawMaterialType = MaterialTypeOf(p),
                        PurchaseDate = p.PurchaseDate,
                        PlannedQuantity = qty,
                        WeighedQuantity = qty,
                        UnitPrice = UnitPriceOf(p),
                        TotalAmount = p.TotalAmount,
                        CreatedByStaffId = p.CreatedBy,
                        CreatedByName = p.CreatedByNavigation?.FullName ?? "Nhân viên thu mua",
                        ApprovedByStaffId = p.ApprovedBy,
                        ApprovedByName = p.ApprovedByNavigation?.FullName,
                        Status = p.Status ?? "Pending",
                        // Không có cột RejectionReason: lý do từ chối nằm trong dòng gắn thẻ của Note
                        Note = NoteTags.Plain(p.Note),
                        RejectionReason = NoteTags.Get(p.Note, NoteTags.Rejected)
                    };
                })
                .ToList();

            // Tab 2: Purchase Reports by Supplier
            var supplierGroups = filtered
                .GroupBy(p => new { p.SupplierId, Name = p.Supplier?.SupplierName ?? "Chưa rõ", Status = p.Supplier?.Status ?? "Active" })
                .ToList();

            decimal totalReportWeight = filtered.Sum(QuantityOf);

            Reports = supplierGroups
                .Select(g =>
                {
                    decimal weight = g.Sum(QuantityOf);
                    decimal spend = g.Sum(x => x.TotalAmount);
                    return new SupplierPurchaseReportItemViewModel
                    {
                        SupplierId = g.Key.SupplierId,
                        SupplierName = g.Key.Name,
                        TripCount = g.Count(),
                        TotalWeightKg = weight,
                        PercentageOfTotal = totalReportWeight > 0 ? Math.Round((weight / totalReportWeight) * 100m, 1) : 0,
                        AverageUnitPrice = weight > 0 ? Math.Round(spend / weight, 0) : 0,
                        TotalAmountVnd = spend,
                        Status = g.Key.Status
                    };
                })
                .OrderByDescending(r => r.TotalWeightKg)
                .ToList();
        }

        public async Task<IActionResult> OnPostApproveOrderAsync(int invoiceId)
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (!IsOwner)
            {
                ErrorMessage = "Chỉ Giám đốc (Owner) mới có quyền phê duyệt đơn mua hàng / hóa đơn nhập.";
                return RedirectToPage(new { Tab, SearchTerm, StatusFilter, FromDate, ToDate });
            }

            var invoice = await _context.PurchaseInvoices.FindAsync(invoiceId);
            if (invoice == null)
            {
                ErrorMessage = "Không tìm thấy đơn mua hàng cần duyệt.";
                return RedirectToPage(new { Tab, SearchTerm, StatusFilter, FromDate, ToDate });
            }

            if (invoice.Status != "Pending" && invoice.Status != "Submitted")
            {
                ErrorMessage = $"Đơn mua hàng {invoice.InvoiceCode} đang ở trạng thái '{invoice.Status}', không thể phê duyệt.";
                return RedirectToPage(new { Tab, SearchTerm, StatusFilter, FromDate, ToDate });
            }

            invoice.Status = "Approved";
            invoice.ApprovedBy = CurrentUserId;
            invoice.Note = NoteTags.Remove(invoice.Note, NoteTags.Rejected);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã phê duyệt thành công đơn mua hàng {invoice.InvoiceCode}. Đã chuyển sang trạng thái chờ nhập kho.";
            return RedirectToPage(new { Tab, SearchTerm, StatusFilter, FromDate, ToDate });
        }

        public async Task<IActionResult> OnPostRejectOrderAsync(int invoiceId, string rejectionReason)
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (!IsOwner)
            {
                ErrorMessage = "Chỉ Giám đốc (Owner) mới có quyền từ chối đơn mua hàng / hóa đơn.";
                return RedirectToPage(new { Tab, SearchTerm, StatusFilter, FromDate, ToDate });
            }

            var invoice = await _context.PurchaseInvoices.FindAsync(invoiceId);
            if (invoice == null)
            {
                ErrorMessage = "Không tìm thấy đơn mua hàng.";
                return RedirectToPage(new { Tab, SearchTerm, StatusFilter, FromDate, ToDate });
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                ErrorMessage = "Vui lòng nhập lý do từ chối đơn mua hàng.";
                return RedirectToPage(new { Tab, SearchTerm, StatusFilter, FromDate, ToDate });
            }

            var reasonError = InputValidator.Text(rejectionReason, "Lý do từ chối", 500);
            if (reasonError != null)
            {
                ErrorMessage = reasonError;
                return RedirectToPage(new { Tab, SearchTerm, StatusFilter, FromDate, ToDate });
            }

            if (invoice.Status != "Pending" && invoice.Status != "Submitted")
            {
                ErrorMessage = $"Đơn mua hàng {invoice.InvoiceCode} đang ở trạng thái '{invoice.Status}', không thể từ chối.";
                return RedirectToPage(new { Tab, SearchTerm, StatusFilter, FromDate, ToDate });
            }

            invoice.Status = "Rejected";
            invoice.ApprovedBy = CurrentUserId;
            invoice.Note = NoteTags.Set(invoice.Note, NoteTags.Rejected, rejectionReason.Trim());
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã từ chối đơn mua hàng {invoice.InvoiceCode}. Lý do: {rejectionReason.Trim()}";
            return RedirectToPage(new { Tab, SearchTerm, StatusFilter, FromDate, ToDate });
        }
    }
}
