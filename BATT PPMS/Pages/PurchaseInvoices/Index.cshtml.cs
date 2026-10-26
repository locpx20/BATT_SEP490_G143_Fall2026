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

        private async Task LoadDataAsync()
        {
            var query = _context.PurchaseInvoices
                .Include(p => p.Supplier)
                .Include(p => p.Creator)
                .Include(p => p.Approver)
                .AsNoTracking()
                .AsQueryable();

            var allInvoices = await query.ToListAsync();

            // Overall KPIs
            TotalSpendVnd = allInvoices.Sum(p => p.TotalAmount);
            TotalWeighedKg = allInvoices.Sum(p => (decimal)(p.WeighedQuantity ?? (p.PlannedQuantity ?? 0)));
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
                    (p.RawMaterialType != null && p.RawMaterialType.ToLowerInvariant().Contains(term)) ||
                    (p.Creator?.FullName != null && p.Creator.FullName.ToLowerInvariant().Contains(term)) ||
                    (p.Note != null && p.Note.ToLowerInvariant().Contains(term))
                );
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "ALL")
            {
                orderFiltered = orderFiltered.Where(p => string.Equals(p.Status, StatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            Orders = orderFiltered
                .OrderByDescending(p => p.PurchaseDate)
                .ThenByDescending(p => p.InvoiceId)
                .Select(p => new PurchaseOrderItemViewModel
                {
                    InvoiceId = p.InvoiceId,
                    InvoiceCode = p.InvoiceCode,
                    SupplierId = p.SupplierId,
                    SupplierName = p.Supplier?.SupplierName ?? "Nhà cung cấp chưa đặt tên",
                    SupplierPhone = p.Supplier?.Phone,
                    SupplierAddress = p.Supplier?.Address,
                    RawMaterialType = !string.IsNullOrWhiteSpace(p.RawMaterialType) ? p.RawMaterialType : "Mùn cưa cao su & Keo",
                    PurchaseDate = p.PurchaseDate,
                    PlannedQuantity = p.PlannedQuantity ?? 0,
                    WeighedQuantity = p.WeighedQuantity ?? (p.PlannedQuantity ?? 0),
                    UnitPrice = p.UnitPrice ?? (p.WeighedQuantity > 0 ? Math.Round(p.TotalAmount / p.WeighedQuantity.Value, 0) : 750),
                    TotalAmount = p.TotalAmount,
                    CreatedByStaffId = p.CreatedBy,
                    CreatedByName = p.Creator?.FullName ?? "Nhân viên thu mua",
                    ApprovedByStaffId = p.ApprovedBy,
                    ApprovedByName = p.Approver?.FullName,
                    Status = p.Status,
                    Note = p.Note,
                    RejectionReason = p.RejectionReason
                })
                .ToList();

            // Tab 2: Purchase Reports by Supplier
            var supplierGroups = filtered
                .GroupBy(p => new { p.SupplierId, Name = p.Supplier?.SupplierName ?? "Chưa rõ", Status = p.Supplier?.Status ?? "Active" })
                .ToList();

            decimal totalReportWeight = filtered.Sum(p => (decimal)(p.WeighedQuantity ?? (p.PlannedQuantity ?? 0)));

            Reports = supplierGroups
                .Select(g =>
                {
                    decimal weight = g.Sum(x => (decimal)(x.WeighedQuantity ?? (x.PlannedQuantity ?? 0)));
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

            invoice.Status = "Approved";
            invoice.ApprovedBy = CurrentUserId;
            invoice.RejectionReason = null;
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

            invoice.Status = "Rejected";
            invoice.ApprovedBy = CurrentUserId;
            invoice.RejectionReason = rejectionReason.Trim();
            await _context.SaveChangesAsync();

            SuccessMessage = $"Đã từ chối đơn mua hàng {invoice.InvoiceCode}. Lý do: {invoice.RejectionReason}";
            return RedirectToPage(new { Tab, SearchTerm, StatusFilter, FromDate, ToDate });
        }
    }
}
