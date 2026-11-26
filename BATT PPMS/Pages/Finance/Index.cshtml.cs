using System.Globalization;
using System.Text.Json;
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
        public bool IsCurrentMonth { get; set; }
        public decimal Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public string? Note { get; set; }
        public List<MonthlyTransactionDetailItem> Details { get; set; } = new();
    }

    public class DebtPaymentEntry
    {
        public string PaymentId { get; set; } = string.Empty;
        public string PartnerType { get; set; } = string.Empty; // customer | supplier
        public int PartnerId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; }
        public string PaidAtIso => PaidAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public string PaidAtDisplay => PaidAt.ToString("HH:mm dd/MM/yyyy");
        public string PaymentMethod { get; set; } = "Chuyển khoản";
        public string? Note { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
    }

    public class PartnerDebtRowViewModel
    {
        public int PartnerId { get; set; }
        public string PartnerType { get; set; } = string.Empty; // customer | supplier
        public string PartnerCode { get; set; } = string.Empty;
        public string PartnerName { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public int OrderOrImportCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount => Math.Max(0m, TotalAmount - PaidAmount);
        public List<DebtPaymentEntry> Payments { get; set; } = new();
    }

    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public IndexModel(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "exports"; // exports | materials | customer-debts | supplier-debts

        [BindProperty(SupportsGet = true)]
        public int Year { get; set; } = DateTime.Now.Year;

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public List<int> AvailableYears { get; set; } = new();
        public List<MonthlyFinanceRow> ExportRows { get; set; } = new();
        public List<MonthlyFinanceRow> MaterialRows { get; set; } = new();

        public List<PartnerDebtRowViewModel> CustomerDebtRows { get; set; } = new();
        public List<PartnerDebtRowViewModel> SupplierDebtRows { get; set; } = new();

        public decimal TotalExportQuantity => ExportRows.Sum(r => r.Quantity);
        public decimal TotalExportAmount => ExportRows.Sum(r => r.TotalAmount);
        public int TotalExportCount => ExportRows.Sum(r => r.TransactionCount);

        public decimal TotalMaterialQuantity => MaterialRows.Sum(r => r.Quantity);
        public decimal TotalMaterialAmount => MaterialRows.Sum(r => r.TotalAmount);
        public int TotalMaterialCount => MaterialRows.Sum(r => r.TransactionCount);

        public decimal TotalCustomerDebtAmount => CustomerDebtRows.Sum(r => r.TotalAmount);
        public decimal TotalCustomerPaidAmount => CustomerDebtRows.Sum(r => r.PaidAmount);
        public decimal TotalCustomerRemainingAmount => CustomerDebtRows.Sum(r => r.RemainingAmount);

        public decimal TotalSupplierDebtAmount => SupplierDebtRows.Sum(r => r.TotalAmount);
        public decimal TotalSupplierPaidAmount => SupplierDebtRows.Sum(r => r.PaidAmount);
        public decimal TotalSupplierRemainingAmount => SupplierDebtRows.Sum(r => r.RemainingAmount);

        public bool CanPayDebt => IsOwner;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        private string DebtStorageFilePath =>
            Path.Combine(_env.ContentRootPath, "App_Data", "debt_payments.json");

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            Tab = NormalizeTab(Tab);

            if (Year < 2020 || Year > 2100)
                Year = DateTime.Now.Year;

            await LoadDataAsync();
            return Page();
        }

        private static string NormalizeTab(string? tab)
        {
            if (string.Equals(tab, "materials", StringComparison.OrdinalIgnoreCase))
                return "materials";
            if (string.Equals(tab, "customer-debts", StringComparison.OrdinalIgnoreCase))
                return "customer-debts";
            if (string.Equals(tab, "supplier-debts", StringComparison.OrdinalIgnoreCase))
                return "supplier-debts";
            return "exports";
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

        public async Task<IActionResult> OnPostRecordPaymentAsync(
            string partnerType,
            int partnerId,
            string? paymentAmount,
            DateTime? paymentDate,
            string? paymentMethod,
            string? paymentNote,
            string? returnTab,
            string? searchTerm)
        {
            var check = RequireRole(RoleConstants.Owner);
            if (check != null) return check;

            var targetTab = NormalizeTab(returnTab ?? (partnerType == "supplier" ? "supplier-debts" : "customer-debts"));
            IActionResult Back() => RedirectToPage(new { tab = targetTab, SearchTerm = searchTerm, year = Year });

            var normType = string.Equals(partnerType, "supplier", StringComparison.OrdinalIgnoreCase)
                ? "supplier"
                : "customer";

            if (!TryParsePositiveDecimal(paymentAmount, out var amount))
            {
                ErrorMessage = "Số tiền thanh toán phải lớn hơn 0.";
                return Back();
            }

            await LoadDataAsync();
            var targetRow = normType == "supplier"
                ? SupplierDebtRows.FirstOrDefault(r => r.PartnerId == partnerId)
                : CustomerDebtRows.FirstOrDefault(r => r.PartnerId == partnerId);

            if (targetRow == null)
            {
                ErrorMessage = "Không tìm thấy đối tác cần thanh toán công nợ.";
                return Back();
            }

            if (targetRow.RemainingAmount <= 0)
            {
                ErrorMessage = $"Công nợ của '{targetRow.PartnerName}' đã được thanh toán đủ.";
                return Back();
            }

            if (amount > targetRow.RemainingAmount)
            {
                ErrorMessage = $"Số tiền thanh toán ({amount:N0} VNĐ) không được vượt quá số tiền còn lại phải trả ({targetRow.RemainingAmount:N0} VNĐ).";
                return Back();
            }

            var allPayments = LoadSavedPayments();
            var seq = allPayments.Count + 1;
            var codePrefix = normType == "supplier" ? "TT-NPP" : "TT-KH";
            var paidTime = paymentDate ?? DateTime.Now;

            allPayments.Add(new DebtPaymentEntry
            {
                PaymentId = $"{codePrefix}-{DateTime.Now:yyMMdd}-{seq:D3}",
                PartnerType = normType,
                PartnerId = partnerId,
                Amount = amount,
                PaidAt = paidTime,
                PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Chuyển khoản" : paymentMethod.Trim(),
                Note = string.IsNullOrWhiteSpace(paymentNote) ? null : paymentNote.Trim(),
                CreatedByName = string.IsNullOrWhiteSpace(CurrentFullName) ? "Quản lý" : CurrentFullName
            });

            SavePayments(allPayments);

            SuccessMessage = $"Đã ghi nhận thanh toán {amount:N0} VNĐ cho {targetRow.PartnerName} ({targetRow.PartnerCode}). Số tiền còn lại phải trả: {(targetRow.RemainingAmount - amount):N0} VNĐ.";
            return Back();
        }

        private List<DebtPaymentEntry> LoadSavedPayments()
        {
            try
            {
                if (!System.IO.File.Exists(DebtStorageFilePath))
                    return new List<DebtPaymentEntry>();

                var json = System.IO.File.ReadAllText(DebtStorageFilePath);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<DebtPaymentEntry>();

                return JsonSerializer.Deserialize<List<DebtPaymentEntry>>(json) ?? new List<DebtPaymentEntry>();
            }
            catch
            {
                return new List<DebtPaymentEntry>();
            }
        }

        private void SavePayments(List<DebtPaymentEntry> entries)
        {
            var dir = Path.GetDirectoryName(DebtStorageFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(DebtStorageFilePath, json);
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
            var allApprovedOrders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Where(o => o.Status != "Pending"
                            && o.Status != "Rejected"
                            && o.Status != "Cancelled")
                .OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.OrderId)
                .ToListAsync();

            var yearOrders = allApprovedOrders.Where(o => o.OrderDate.Year == Year).ToList();

            var currentMonth = DateTime.Now.Month;
            var orderedMonths = new[] { currentMonth }
                .Concat(Enumerable.Range(1, 12).Where(m => m != currentMonth))
                .ToList();

            var exportByMonth = yearOrders
                .GroupBy(o => o.OrderDate.Month)
                .ToDictionary(g => g.Key, g => g.ToList());

            ExportRows = orderedMonths.Select(m =>
            {
                exportByMonth.TryGetValue(m, out var list);
                list ??= new List<Order>();
                var cnt = list.Count;
                return new MonthlyFinanceRow
                {
                    Month = m,
                    IsCurrentMonth = (m == currentMonth),
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
            var allApprovedImports = await _context.RawMaterialImports
                .AsNoTracking()
                .Include(i => i.Supplier)
                .Include(i => i.Material)
                .Include(i => i.Receipt)
                .Where(i => i.Status == null || i.Status == "Approved")
                .OrderByDescending(i => i.ImportDate)
                .ThenByDescending(i => i.ImportId)
                .ToListAsync();

            var yearImports = allApprovedImports.Where(i => i.ImportDate.Year == Year).ToList();

            var materialByMonth = yearImports
                .GroupBy(i => i.ImportDate.Month)
                .ToDictionary(g => g.Key, g => g.ToList());

            MaterialRows = orderedMonths.Select(m =>
            {
                materialByMonth.TryGetValue(m, out var list);
                list ??= new List<RawMaterialImport>();
                var cnt = list.Count;
                return new MonthlyFinanceRow
                {
                    Month = m,
                    IsCurrentMonth = (m == currentMonth),
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

            // 3. Bảng Công nợ Khách hàng & Công nợ Nhà phân phối (Nhà cung cấp)
            var savedPayments = LoadSavedPayments();

            var customers = await _context.Customers
                .AsNoTracking()
                .OrderBy(c => c.CustomerId)
                .ToListAsync();

            var ordersByCustomer = allApprovedOrders
                .Where(o => o.CustomerId.HasValue)
                .GroupBy(o => o.CustomerId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var customerPaymentsById = savedPayments
                .Where(p => string.Equals(p.PartnerType, "customer", StringComparison.OrdinalIgnoreCase))
                .GroupBy(p => p.PartnerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.PaidAt).ToList());

            var allCustomerRows = customers.Select(c =>
            {
                ordersByCustomer.TryGetValue(c.CustomerId, out var custOrders);
                custOrders ??= new List<Order>();
                customerPaymentsById.TryGetValue(c.CustomerId, out var custPayments);
                custPayments ??= new List<DebtPaymentEntry>();

                var totalAmt = custOrders.Sum(o => o.TotalAmount);
                var paidAmt = Math.Min(totalAmt, custPayments.Sum(p => p.Amount));

                return new PartnerDebtRowViewModel
                {
                    PartnerId = c.CustomerId,
                    PartnerType = "customer",
                    PartnerCode = $"KH-{c.CustomerId:D2}",
                    PartnerName = c.CustomerName,
                    ContactPerson = c.ContactPerson,
                    Phone = c.Phone,
                    OrderOrImportCount = custOrders.Count,
                    TotalAmount = totalAmt,
                    PaidAmount = paidAmt,
                    Payments = custPayments
                };
            }).ToList();

            var suppliers = await _context.Suppliers
                .AsNoTracking()
                .OrderBy(s => s.SupplierId)
                .ToListAsync();

            var importsBySupplier = allApprovedImports
                .GroupBy(i => i.SupplierId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var supplierPaymentsById = savedPayments
                .Where(p => string.Equals(p.PartnerType, "supplier", StringComparison.OrdinalIgnoreCase))
                .GroupBy(p => p.PartnerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.PaidAt).ToList());

            var allSupplierRows = suppliers.Select(s =>
            {
                importsBySupplier.TryGetValue(s.SupplierId, out var supImports);
                supImports ??= new List<RawMaterialImport>();
                supplierPaymentsById.TryGetValue(s.SupplierId, out var supPayments);
                supPayments ??= new List<DebtPaymentEntry>();

                var totalAmt = supImports.Sum(i => i.Quantity * i.UnitPrice);
                var paidAmt = Math.Min(totalAmt, supPayments.Sum(p => p.Amount));

                return new PartnerDebtRowViewModel
                {
                    PartnerId = s.SupplierId,
                    PartnerType = "supplier",
                    PartnerCode = $"NPP-{s.SupplierId:D2}",
                    PartnerName = s.SupplierName,
                    ContactPerson = s.ContactPerson,
                    Phone = s.Phone,
                    OrderOrImportCount = supImports.Count,
                    TotalAmount = totalAmt,
                    PaidAmount = paidAmt,
                    Payments = supPayments
                };
            }).ToList();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var kw = SearchTerm.Trim().ToLowerInvariant();
                if (Tab == "customer-debts")
                {
                    allCustomerRows = allCustomerRows.Where(r =>
                        r.PartnerName.ToLowerInvariant().Contains(kw) ||
                        r.PartnerCode.ToLowerInvariant().Contains(kw) ||
                        (r.ContactPerson != null && r.ContactPerson.ToLowerInvariant().Contains(kw)) ||
                        (r.Phone != null && r.Phone.Contains(kw))).ToList();
                }
                else if (Tab == "supplier-debts")
                {
                    allSupplierRows = allSupplierRows.Where(r =>
                        r.PartnerName.ToLowerInvariant().Contains(kw) ||
                        r.PartnerCode.ToLowerInvariant().Contains(kw) ||
                        (r.ContactPerson != null && r.ContactPerson.ToLowerInvariant().Contains(kw)) ||
                        (r.Phone != null && r.Phone.Contains(kw))).ToList();
                }
            }

            CustomerDebtRows = allCustomerRows;
            SupplierDebtRows = allSupplierRows;
        }
    }
}
