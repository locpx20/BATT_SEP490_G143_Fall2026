using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Suppliers
{
    public class SupplierViewModel
    {
        public int SupplierId { get; set; }
        public string Code => $"NCC-{SupplierId:D2}";
        public string Name { get; set; } = "";
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string Status { get; set; } = "Active";
        public int TotalPurchasesCount { get; set; }
        public decimal TotalPurchasesKg { get; set; }
        public decimal TotalPaidVnd { get; set; }
        public List<SupplierPurchaseItemViewModel> RecentPurchases { get; set; } = new();
    }

    public class SupplierPurchaseItemViewModel
    {
        public string Code { get; set; } = "";
        public DateTime Date { get; set; }
        public decimal Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "";
    }

    public class CustomerViewModel
    {
        public int CustomerId { get; set; }
        public string Code => $"KH-{CustomerId:D2}";
        public string Name { get; set; } = "";
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string Status { get; set; } = "Active";
        public int TotalOrdersCount { get; set; }
        public decimal TotalOrderedKg { get; set; }
        public decimal TotalRevenueVnd { get; set; }
        public List<CustomerOrderItemViewModel> RecentOrders { get; set; } = new();
    }

    public class CustomerOrderItemViewModel
    {
        public string OrderCode { get; set; } = "";
        public DateTime OrderDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "";
    }

    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        // Active tab: "suppliers" or "customers"
        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "suppliers";

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public List<SupplierViewModel> Suppliers { get; set; } = new();
        public List<CustomerViewModel> Customers { get; set; } = new();

        public bool CanManage => IsOwner;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (string.IsNullOrWhiteSpace(Tab))
                Tab = "suppliers";
            Tab = Tab.ToLowerInvariant();

            await LoadDataAsync();
            return Page();
        }

        // ==========================================
        // SUPPLIER HANDLERS
        // ==========================================
        public async Task<IActionResult> OnPostCreateSupplierAsync(
            string SupplierName, string? ContactPerson, string? Phone, string? Email, string? Address)
        {
            var check = RequireRole(RoleConstants.Owner);
            if (check != null) return check;

            if (string.IsNullOrWhiteSpace(SupplierName))
            {
                ErrorMessage = "Vui lòng nhập tên Nhà cung cấp.";
                return RedirectToPage(new { Tab = "suppliers" });
            }

            var creatorId = CurrentUserId > 0 ? CurrentUserId : 2; // fallback to Owner id

            var supplier = new Supplier
            {
                SupplierName = SupplierName.Trim(),
                ContactPerson = ContactPerson?.Trim(),
                Phone = Phone?.Trim(),
                Email = Email?.Trim(),
                Address = Address?.Trim(),
                Status = StatusConstants.Active,
                CreatedBy = creatorId
            };

            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Thêm mới Nhà cung cấp '{supplier.SupplierName}' (NCC-{supplier.SupplierId:D2}) thành công!";
            return RedirectToPage(new { Tab = "suppliers" });
        }

        public async Task<IActionResult> OnPostUpdateSupplierAsync(
            int SupplierId, string SupplierName, string? ContactPerson, string? Phone, string? Email, string? Address, string? Status)
        {
            var check = RequireRole(RoleConstants.Owner);
            if (check != null) return check;

            var supplier = await _context.Suppliers.FindAsync(SupplierId);
            if (supplier == null)
            {
                ErrorMessage = "Không tìm thấy Nhà cung cấp cần cập nhật.";
                return RedirectToPage(new { Tab = "suppliers" });
            }

            if (string.IsNullOrWhiteSpace(SupplierName))
            {
                ErrorMessage = "Tên Nhà cung cấp không được để trống.";
                return RedirectToPage(new { Tab = "suppliers" });
            }

            supplier.SupplierName = SupplierName.Trim();
            supplier.ContactPerson = ContactPerson?.Trim();
            supplier.Phone = Phone?.Trim();
            supplier.Email = Email?.Trim();
            supplier.Address = Address?.Trim();
            if (!string.IsNullOrWhiteSpace(Status))
                supplier.Status = Status;

            await _context.SaveChangesAsync();

            SuccessMessage = $"Cập nhật thông tin Nhà cung cấp NCC-{supplier.SupplierId:D2} thành công!";
            return RedirectToPage(new { Tab = "suppliers" });
        }

        public async Task<IActionResult> OnPostToggleSupplierStatusAsync(int SupplierId)
        {
            var check = RequireRole(RoleConstants.Owner);
            if (check != null) return check;

            var supplier = await _context.Suppliers.FindAsync(SupplierId);
            if (supplier == null)
            {
                ErrorMessage = "Không tìm thấy Nhà cung cấp.";
                return RedirectToPage(new { Tab = "suppliers" });
            }

            var isCurrentlyActive = supplier.Status == StatusConstants.Active;
            supplier.Status = isCurrentlyActive ? StatusConstants.Inactive : StatusConstants.Active;
            await _context.SaveChangesAsync();

            var actionName = isCurrentlyActive ? "ngừng hợp tác với" : "kích hoạt lại";
            SuccessMessage = $"Đã {actionName} Nhà cung cấp '{supplier.SupplierName}' (NCC-{supplier.SupplierId:D2}).";
            return RedirectToPage(new { Tab = "suppliers" });
        }

        // ==========================================
        // CUSTOMER HANDLERS
        // ==========================================
        public async Task<IActionResult> OnPostCreateCustomerAsync(
            string CustomerName, string? ContactPerson, string? Phone, string? Email, string? Address)
        {
            var check = RequireRole(RoleConstants.Owner);
            if (check != null) return check;

            if (string.IsNullOrWhiteSpace(CustomerName))
            {
                ErrorMessage = "Vui lòng nhập tên Khách hàng.";
                return RedirectToPage(new { Tab = "customers" });
            }

            var creatorId = CurrentUserId > 0 ? CurrentUserId : 2;

            var customer = new Customer
            {
                CustomerName = CustomerName.Trim(),
                ContactPerson = ContactPerson?.Trim(),
                Phone = Phone?.Trim(),
                Email = Email?.Trim(),
                Address = Address?.Trim(),
                Status = StatusConstants.Active,
                CreatedBy = creatorId
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Thêm mới Khách hàng '{customer.CustomerName}' (KH-{customer.CustomerId:D2}) thành công!";
            return RedirectToPage(new { Tab = "customers" });
        }

        public async Task<IActionResult> OnPostUpdateCustomerAsync(
            int CustomerId, string CustomerName, string? ContactPerson, string? Phone, string? Email, string? Address, string? Status)
        {
            var check = RequireRole(RoleConstants.Owner);
            if (check != null) return check;

            var customer = await _context.Customers.FindAsync(CustomerId);
            if (customer == null)
            {
                ErrorMessage = "Không tìm thấy Khách hàng cần cập nhật.";
                return RedirectToPage(new { Tab = "customers" });
            }

            if (string.IsNullOrWhiteSpace(CustomerName))
            {
                ErrorMessage = "Tên Khách hàng không được để trống.";
                return RedirectToPage(new { Tab = "customers" });
            }

            customer.CustomerName = CustomerName.Trim();
            customer.ContactPerson = ContactPerson?.Trim();
            customer.Phone = Phone?.Trim();
            customer.Email = Email?.Trim();
            customer.Address = Address?.Trim();
            if (!string.IsNullOrWhiteSpace(Status))
                customer.Status = Status;

            await _context.SaveChangesAsync();

            SuccessMessage = $"Cập nhật thông tin Khách hàng KH-{customer.CustomerId:D2} thành công!";
            return RedirectToPage(new { Tab = "customers" });
        }

        public async Task<IActionResult> OnPostToggleCustomerStatusAsync(int CustomerId)
        {
            var check = RequireRole(RoleConstants.Owner);
            if (check != null) return check;

            var customer = await _context.Customers.FindAsync(CustomerId);
            if (customer == null)
            {
                ErrorMessage = "Không tìm thấy Khách hàng.";
                return RedirectToPage(new { Tab = "customers" });
            }

            var isCurrentlyActive = customer.Status == StatusConstants.Active;
            customer.Status = isCurrentlyActive ? StatusConstants.Inactive : StatusConstants.Active;
            await _context.SaveChangesAsync();

            var actionName = isCurrentlyActive ? "ngừng hợp tác với" : "kích hoạt lại";
            SuccessMessage = $"Đã {actionName} Khách hàng '{customer.CustomerName}' (KH-{customer.CustomerId:D2}).";
            return RedirectToPage(new { Tab = "customers" });
        }

        // ==========================================
        // DATA LOADER
        // ==========================================
        private async Task LoadDataAsync()
        {
            // Load Suppliers
            var supQuery = _context.Suppliers
                .Include(s => s.PurchaseInvoices)
                    .ThenInclude(p => p.PurchaseInvoiceDetails)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var kw = SearchTerm.Trim().ToLower();
                supQuery = supQuery.Where(s =>
                    s.SupplierName.ToLower().Contains(kw) ||
                    (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(kw)) ||
                    (s.Phone != null && s.Phone.Contains(kw)) ||
                    (s.Email != null && s.Email.ToLower().Contains(kw)) ||
                    (s.Address != null && s.Address.ToLower().Contains(kw)));
            }

            var rawSuppliers = await supQuery.OrderBy(s => s.SupplierId).ToListAsync();
            Suppliers = rawSuppliers.Select(s => {
                var invoices = s.PurchaseInvoices.ToList();
                var totalPaid = invoices.Sum(i => i.TotalAmount);
                var totalKg = invoices.SelectMany(i => i.PurchaseInvoiceDetails).Sum(d => (decimal)d.Quantity);

                return new SupplierViewModel
                {
                    SupplierId = s.SupplierId,
                    Name = s.SupplierName,
                    ContactPerson = s.ContactPerson ?? "—",
                    Phone = s.Phone ?? "—",
                    Email = s.Email ?? "—",
                    Address = s.Address ?? "—",
                    Status = s.Status,
                    TotalPurchasesCount = invoices.Count,
                    TotalPurchasesKg = totalKg,
                    TotalPaidVnd = totalPaid,
                    RecentPurchases = invoices.OrderByDescending(i => i.PurchaseDate).Take(10).Select(i => new SupplierPurchaseItemViewModel
                    {
                        Code = i.InvoiceCode,
                        Date = i.PurchaseDate,
                        Quantity = i.PurchaseInvoiceDetails.Sum(d => (decimal)d.Quantity),
                        TotalAmount = i.TotalAmount,
                        Status = i.Status
                    }).ToList()
                };
            }).ToList();

            // Load Customers
            var cusQuery = _context.Customers
                .Include(c => c.Orders)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var kw = SearchTerm.Trim().ToLower();
                cusQuery = cusQuery.Where(c =>
                    c.CustomerName.ToLower().Contains(kw) ||
                    (c.ContactPerson != null && c.ContactPerson.ToLower().Contains(kw)) ||
                    (c.Phone != null && c.Phone.Contains(kw)) ||
                    (c.Email != null && c.Email.ToLower().Contains(kw)) ||
                    (c.Address != null && c.Address.ToLower().Contains(kw)));
            }

            var rawCustomers = await cusQuery.OrderBy(c => c.CustomerId).ToListAsync();
            Customers = rawCustomers.Select(c => {
                var orders = c.Orders.ToList();
                var totalRevenue = orders.Sum(o => o.TotalAmount);
                var totalKg = orders.Sum(o => (decimal)o.Quantity);

                return new CustomerViewModel
                {
                    CustomerId = c.CustomerId,
                    Name = c.CustomerName,
                    ContactPerson = c.ContactPerson ?? "—",
                    Phone = c.Phone ?? "—",
                    Email = c.Email ?? "—",
                    Address = c.Address ?? "—",
                    Status = c.Status,
                    TotalOrdersCount = orders.Count,
                    TotalOrderedKg = totalKg,
                    TotalRevenueVnd = totalRevenue,
                    RecentOrders = orders.OrderByDescending(o => o.OrderDate).Take(10).Select(o => new CustomerOrderItemViewModel
                    {
                        OrderCode = o.OrderCode,
                        OrderDate = o.OrderDate,
                        Quantity = o.Quantity,
                        UnitPrice = o.UnitPrice,
                        TotalAmount = o.TotalAmount,
                        Status = o.Status
                    }).ToList()
                };
            }).ToList();
        }
    }
}
