using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using BATT_PPMS.Pages.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Deliveries
{
    /// <summary>Screen: Danh sách yêu cầu xuất kho (Manager tạo, thủ kho kiểm tra, Owner xem).</summary>
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

        public List<Delivery> Deliveries { get; private set; } = new();

        /// <summary>Yêu cầu bán đã duyệt nhưng Manager chưa tạo yêu cầu xuất kho.</summary>
        public List<Order> ReadyOrders { get; private set; } = new();

        public int TotalCount { get; private set; }
        public int TotalPages { get; private set; }
        public int WaitingCheckCount { get; private set; }

        public bool CanManage => IsManager;
        public bool IsWarehouse => IsWarehouseStaff;

        public static readonly string[] FilterStatuses =
            { StatusConstants.Prepared, "Returned", StatusConstants.Delivered };

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Manager, RoleConstants.WarehouseStaff, RoleConstants.Owner);
            if (check != null) return check;

            WaitingCheckCount = await _context.Deliveries.CountAsync(d =>
                d.DeliveryStatus == StatusConstants.Prepared && d.ExportSlip == null);

            if (IsManager)
            {
                ReadyOrders = await _context.Orders.AsNoTracking()
                    .Include(o => o.Customer)
                    .Where(o => o.Status == StatusConstants.Approved
                                && !o.Deliveries.Any(d => d.DeliveryStatus != StatusConstants.Cancelled))
                    .OrderBy(o => o.OrderDate)
                    .ToListAsync();
            }

            var query = _context.Deliveries.AsNoTracking()
                .Include(d => d.Order).ThenInclude(o => o.Customer)
                .Include(d => d.ExportSlip)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim();
                query = query.Where(d => d.Order.OrderCode.Contains(term) || d.DriverName.Contains(term) || d.LicensePlate.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(Status))
                query = query.Where(d => d.DeliveryStatus == Status);

            TotalCount = await query.CountAsync();
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
            PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

            Deliveries = await query
                .OrderByDescending(d => d.DeliveryStatus == StatusConstants.Prepared && d.ExportSlip == null)
                .ThenByDescending(d => d.PickupDate)
                .ThenByDescending(d => d.DeliveryId)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            return Page();
        }
    }
}
