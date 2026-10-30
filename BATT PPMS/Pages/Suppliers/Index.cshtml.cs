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

        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "suppliers";

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
        [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
        public PageWindow PartnerPagination { get; private set; } = new(0, 1);

        public List<SupplierViewModel> Suppliers { get; set; } = new();
        public List<CustomerViewModel> Customers { get; set; } = new();

        public bool CanManage => IsOwner;
        public bool CanViewCustomers => !IsPurchasingStaff;

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
            Tab = Tab.Trim().Equals("customers", StringComparison.OrdinalIgnoreCase) ? "customers" : "suppliers";

            if (!CanViewCustomers)
                Tab = "suppliers";

            await LoadDataAsync();
            PageNumber = PageWindow.ReadRequestedPage(Request.Query, PageNumber);
            PartnerPagination = new PageWindow(Tab == "customers" ? Customers.Count : Suppliers.Count, PageNumber);
            PageNumber = PartnerPagination.CurrentPage;
            return Page();
        }

        // ==========================================
        // VALIDATION
        // ==========================================
        private static string? ValidatePartner(string partnerLabel, string? name, string? contact,
            string? phone, string? email, string? address, string? status) =>
            InputValidator.First(
                InputValidator.Text(name, $"Tên {partnerLabel}", 100, required: true),
                InputValidator.Text(contact, "Người liên hệ", 100),
                InputValidator.Phone(phone),
                InputValidator.Email(email),
                InputValidator.Text(address, "Địa chỉ", 500),
                InputValidator.Status(status, StatusSets.PartnerStatuses, "Trạng thái hợp tác"));

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

            var supErr = ValidatePartner("Nhà cung cấp", SupplierName, ContactPerson, Phone, Email, Address, null);
            if (supErr == null && await _context.Suppliers.AnyAsync(s => s.SupplierName.ToLower() == SupplierName.Trim().ToLower()))
                supErr = $"Nhà cung cấp '{SupplierName.Trim()}' đã tồn tại.";
            if (supErr != null)
            {
                ErrorMessage = supErr;
                return RedirectToPage(new { Tab = "suppliers" });
            }

            var creatorId = CurrentUserId > 0 ? CurrentUserId : 2;

            var supplier = new Supplier
            {
                SupplierName = SupplierName.Trim(),
                ContactPerson = InputValidator.Clean(ContactPerson),
                Phone = InputValidator.Clean(Phone),
                Email = InputValidator.Clean(Email),
                Address = InputValidator.Clean(Address),
                Status = StatusConstants.Active,
                CreatedBy = creatorId
            };

            _context.Suppliers.Add(supplier);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Tạo Nhà cung cấp thành công: Đã thêm NCC-{supplier.SupplierId:D2} - {supplier.SupplierName}";
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

            var supUpdErr = ValidatePartner("Nhà cung cấp", SupplierName, ContactPerson, Phone, Email, Address, Status);
            if (supUpdErr == null && await _context.Suppliers.AnyAsync(s => s.SupplierId != SupplierId && s.SupplierName.ToLower() == SupplierName.Trim().ToLower()))
                supUpdErr = $"Nhà cung cấp '{SupplierName.Trim()}' đã tồn tại.";
            if (supUpdErr != null)
            {
                ErrorMessage = supUpdErr;
                return RedirectToPage(new { Tab = "suppliers" });
            }

            supplier.SupplierName = SupplierName.Trim();
            supplier.ContactPerson = InputValidator.Clean(ContactPerson);
            supplier.Phone = InputValidator.Clean(Phone);
            supplier.Email = InputValidator.Clean(Email);
            supplier.Address = InputValidator.Clean(Address);
            if (!string.IsNullOrWhiteSpace(Status))
                supplier.Status = Status;

            await _context.SaveChangesAsync();

            SuccessMessage = $"Cập nhật thành công: Đã lưu thay đổi cho NCC NCC-{supplier.SupplierId:D2}";
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

            var actionLabel = isCurrentlyActive ? "ngừng sử dụng" : "kích hoạt lại";
            SuccessMessage = $"Thao tác thành công: Đã {actionLabel} NCC NCC-{supplier.SupplierId:D2}";
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

            var cusErr = ValidatePartner("Khách hàng", CustomerName, ContactPerson, Phone, Email, Address, null);
            if (cusErr == null && await _context.Customers.AnyAsync(c => c.CustomerName.ToLower() == CustomerName.Trim().ToLower()))
                cusErr = $"Khách hàng '{CustomerName.Trim()}' đã tồn tại.";
            if (cusErr != null)
            {
                ErrorMessage = cusErr;
                return RedirectToPage(new { Tab = "customers" });
            }

            var creatorId = CurrentUserId > 0 ? CurrentUserId : 2;

            var customer = new Customer
            {
                CustomerName = CustomerName.Trim(),
                ContactPerson = InputValidator.Clean(ContactPerson),
                Phone = InputValidator.Clean(Phone),
                Email = InputValidator.Clean(Email),
                Address = InputValidator.Clean(Address),
                Status = StatusConstants.Active,
                CreatedBy = creatorId
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            SuccessMessage = $"Tạo Khách hàng thành công: Đã thêm KH-{customer.CustomerId:D2} - {customer.CustomerName}";
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

            var cusUpdErr = ValidatePartner("Khách hàng", CustomerName, ContactPerson, Phone, Email, Address, Status);
            if (cusUpdErr == null && await _context.Customers.AnyAsync(c => c.CustomerId != CustomerId && c.CustomerName.ToLower() == CustomerName.Trim().ToLower()))
                cusUpdErr = $"Khách hàng '{CustomerName.Trim()}' đã tồn tại.";
            if (cusUpdErr != null)
            {
                ErrorMessage = cusUpdErr;
                return RedirectToPage(new { Tab = "customers" });
            }

            customer.CustomerName = CustomerName.Trim();
            customer.ContactPerson = InputValidator.Clean(ContactPerson);
            customer.Phone = InputValidator.Clean(Phone);
            customer.Email = InputValidator.Clean(Email);
            customer.Address = InputValidator.Clean(Address);
            if (!string.IsNullOrWhiteSpace(Status))
                customer.Status = Status;

            await _context.SaveChangesAsync();

            SuccessMessage = $"Cập nhật thành công: Đã lưu thay đổi cho Khách hàng KH-{customer.CustomerId:D2}";
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

            var actionLabel = isCurrentlyActive ? "ngừng sử dụng" : "kích hoạt lại";
            SuccessMessage = $"Thao tác thành công: Đã {actionLabel} Khách hàng KH-{customer.CustomerId:D2}";
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
                    s.SupplierName.Trim().ToLower() == kw ||
                    (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(kw)) ||
                    (s.Phone != null && s.Phone.Contains(kw)) ||
                    (s.Email != null && s.Email.ToLower().Contains(kw)) ||
                    (s.Address != null && s.Address.ToLower().Contains(kw)));
            }

            if (IsOwner && StatusFilter is "Active" or "Inactive")
                supQuery = supQuery.Where(s => s.Status == StatusFilter);
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
                    Status = s.Status ?? StatusConstants.Active,
                    TotalPurchasesCount = invoices.Count,
                    TotalPurchasesKg = totalKg,
                    TotalPaidVnd = totalPaid,
                    RecentPurchases = invoices.OrderByDescending(i => i.PurchaseDate).Take(10).Select(i => new SupplierPurchaseItemViewModel
                    {
                        Code = i.InvoiceCode,
                        Date = i.PurchaseDate,
                        Quantity = i.PurchaseInvoiceDetails.Sum(d => (decimal)d.Quantity),
                        TotalAmount = i.TotalAmount,
                        Status = i.Status ?? ""
                    }).ToList()
                };
            }).ToList();

            // Purchasing Staff không được xem danh sách Khách hàng -> không load dữ liệu
            if (!CanViewCustomers) return;

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

            if (IsOwner && StatusFilter is "Active" or "Inactive")
                cusQuery = cusQuery.Where(c => c.Status == StatusFilter);
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
                    Status = c.Status ?? StatusConstants.Active,
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
                        Status = o.Status ?? ""
                    }).ToList()
                };
            }).ToList();
        }
    }
}
