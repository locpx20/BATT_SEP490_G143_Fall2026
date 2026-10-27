using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Orders
{
    /// <summary>Screen: Order List (Manager).</summary>
    public class IndexModel : BasePageModel
    {
        public const int PageSize = 10;

        private readonly ProductionManagementDbContext _context;

        public IndexModel(ProductionManagementDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Status { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public List<Order> Orders { get; private set; } = new();
        public int TotalCount { get; private set; }
        public int TotalPages { get; private set; }

        /// <summary>Chỉ Manager được tạo / sửa / xoá; Owner chỉ xem.</summary>
        public bool CanManage => IsManager;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            var query = _context.Orders.AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.Inventory)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim();
                query = query.Where(o => o.OrderCode.Contains(term) || o.Customer.CustomerName.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(Status))
                query = query.Where(o => o.Status == Status);

            TotalCount = await query.CountAsync();
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
            PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

            Orders = await query
                .OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.OrderId)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            return Page();
        }
    }
}
