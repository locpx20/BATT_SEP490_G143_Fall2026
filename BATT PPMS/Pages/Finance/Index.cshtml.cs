using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Finance
{
    public class MonthlyTransactionDetailItem
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? SubCode { get; set; }
        public string DateText { get; set; } = string.Empty;
        public string PartnerName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public string StatusText { get; set; } = string.Empty;
    }

    public class MonthlyFinanceRow
    {
        public int Month { get; set; }
        public string MonthLabel => $"THÁNG {Month}";
        public decimal Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public string? Note { get; set; }
        public List<MonthlyTransactionDetailItem> Details { get; set; } = new();
    }

    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "exports"; // exports | materials

        [BindProperty(SupportsGet = true)]
        public int Year { get; set; } = DateTime.Now.Year;

        public List<int> AvailableYears { get; set; } = new();
        public List<MonthlyFinanceRow> ExportRows { get; set; } = new();
        public List<MonthlyFinanceRow> MaterialRows { get; set; } = new();

        public decimal TotalExportQuantity => ExportRows.Sum(r => r.Quantity);
        public decimal TotalExportAmount => ExportRows.Sum(r => r.TotalAmount);
        public int TotalExportCount => ExportRows.Sum(r => r.TransactionCount);

        public decimal TotalMaterialQuantity => MaterialRows.Sum(r => r.Quantity);
        public decimal TotalMaterialAmount => MaterialRows.Sum(r => r.TotalAmount);
        public int TotalMaterialCount => MaterialRows.Sum(r => r.TransactionCount);

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (!string.Equals(Tab, "materials", StringComparison.OrdinalIgnoreCase))
                Tab = "exports";
            else
                Tab = "materials";

            if (Year < 2020 || Year > 2100)
                Year = DateTime.Now.Year;

            await LoadDataAsync();
            return Page();
        }

        private async Task LoadDataAsync()
        {
            var currentYear = DateTime.Now.Year;
            var orderYears = await _context.Orders
                .AsNoTracking()
                .Select(o => o.OrderDate.Year)
                .Distinct()
                .ToListAsync();

            var importYears = await _context.RawMaterialImports
                .AsNoTracking()
                .Select(i => i.ImportDate.Year)
                .Distinct()
                .ToListAsync();

            AvailableYears = orderYears
                .Concat(importYears)
                .Append(currentYear)
                .Append(2026)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            // 1. Bảng Tổng hợp hàng xuất theo 12 tháng của năm được chọn
            var yearOrders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Where(o => o.OrderDate.Year == Year
                            && o.Status != "Pending"
                            && o.Status != "Rejected"
                            && o.Status != "Cancelled")
                .OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.OrderId)
                .ToListAsync();

            var exportByMonth = yearOrders
                .GroupBy(o => o.OrderDate.Month)
                .ToDictionary(g => g.Key, g => g.ToList());

            ExportRows = Enumerable.Range(1, 12).Select(m =>
            {
                exportByMonth.TryGetValue(m, out var list);
                list ??= new List<Order>();
                var cnt = list.Count;
                return new MonthlyFinanceRow
                {
                    Month = m,
                    Quantity = list.Sum(o => (decimal)o.Quantity),
                    TotalAmount = list.Sum(o => o.TotalAmount),
                    TransactionCount = cnt,
                    Note = cnt > 0 ? $"{cnt} đơn hàng xuất" : "",
                    Details = list.Select(o => new MonthlyTransactionDetailItem
                    {
                        Id = o.OrderId,
                        Code = o.OrderCode,
                        DateText = o.OrderDate.ToString("dd/MM/yyyy HH:mm"),
                        PartnerName = o.Customer?.CustomerName ?? "—",
                        ItemName = string.IsNullOrWhiteSpace(o.Grade) ? "Viên nén thành phẩm" : $"Viên nén Loại {o.Grade}",
                        Quantity = o.Quantity,
                        UnitPrice = o.UnitPrice ?? (o.Quantity > 0 ? Math.Round(o.TotalAmount / o.Quantity, 0) : 0m),
                        TotalAmount = o.TotalAmount,
                        StatusText = o.Status switch
                        {
                            "Completed" => "Đã hoàn tất",
                            "Approved" => "Đã duyệt",
                            _ => o.Status ?? "Đã duyệt"
                        }
                    }).ToList()
                };
            }).ToList();

            // 2. Bảng Tổng hợp nguyên liệu (TH nguyên liệu) theo 12 tháng của năm được chọn
            var yearImports = await _context.RawMaterialImports
                .AsNoTracking()
                .Include(i => i.Supplier)
                .Include(i => i.Material)
                .Include(i => i.Receipt)
                .Where(i => i.ImportDate.Year == Year
                            && (i.Status == null || i.Status == "Approved"))
                .OrderByDescending(i => i.ImportDate)
                .ThenByDescending(i => i.ImportId)
                .ToListAsync();

            var materialByMonth = yearImports
                .GroupBy(i => i.ImportDate.Month)
                .ToDictionary(g => g.Key, g => g.ToList());

            MaterialRows = Enumerable.Range(1, 12).Select(m =>
            {
                materialByMonth.TryGetValue(m, out var list);
                list ??= new List<RawMaterialImport>();
                var cnt = list.Count;
                return new MonthlyFinanceRow
                {
                    Month = m,
                    Quantity = list.Sum(i => i.Quantity),
                    TotalAmount = list.Sum(i => i.Quantity * i.UnitPrice),
                    TransactionCount = cnt,
                    Note = cnt > 0 ? $"{cnt} phiếu nhập kho (PNK)" : "",
                    Details = list.Select(i => new MonthlyTransactionDetailItem
                    {
                        Id = i.ImportId,
                        Code = string.IsNullOrWhiteSpace(i.ImportCode) ? $"PNK-26{i.ImportId:D3}" : i.ImportCode!,
                        SubCode = i.Receipt?.ReceiptCode,
                        DateText = i.ImportDate.ToString("dd/MM/yyyy HH:mm"),
                        PartnerName = i.Supplier?.SupplierName ?? "—",
                        ItemName = i.Material?.MaterialName ?? "Nguyên liệu thô",
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalAmount = i.Quantity * i.UnitPrice,
                        StatusText = "Đã nhập kho"
                    }).ToList()
                };
            }).ToList();
        }
    }
}
