using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.ExportSlips
{
    /// <summary>Screen: Danh sách phiếu xuất kho thành phẩm (thủ kho, Manager, Owner).</summary>
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

        /// <summary>Lọc từ ngày (yyyy-MM-dd), bao gồm cả ngày này.</summary>
        [BindProperty(SupportsGet = true)]
        public string? FromDate { get; set; }

        /// <summary>Lọc đến ngày (yyyy-MM-dd), bao gồm cả ngày này.</summary>
        [BindProperty(SupportsGet = true)]
        public string? ToDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public List<ExportSlip> Slips { get; private set; } = new();
        public int TotalCount { get; private set; }
        public int TotalPages { get; private set; }
        public int ProcessingCount { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.WarehouseStaff, RoleConstants.Manager, RoleConstants.Owner);
            if (check != null) return check;

            ProcessingCount = await _context.ExportSlips.CountAsync(s => s.Status == ExportSlipStatus.Processing);

            var query = _context.ExportSlips.AsNoTracking()
                .Include(s => s.Order).ThenInclude(o => o.Customer)
                .Include(s => s.Delivery)
                .Include(s => s.Lines)
                .AsSplitQuery()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim();
                query = query.Where(s => s.SlipCode.Contains(term) || s.Order.OrderCode.Contains(term)
                    || s.Delivery.DriverName.Contains(term) || s.Delivery.LicensePlate.Contains(term));
            }

            if (Status is ExportSlipStatus.Processing or ExportSlipStatus.Completed)
                query = query.Where(s => s.Status == Status);

            // Lọc theo khoảng ngày; nếu nhập ngược (từ > đến) thì tự đổi chỗ
            var fromDate = ParseDate(FromDate);
            var toDate = ParseDate(ToDate);
            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);
            FromDate = fromDate?.ToString("yyyy-MM-dd");
            ToDate = toDate?.ToString("yyyy-MM-dd");
            if (fromDate.HasValue) query = query.Where(s => s.CreatedAt >= fromDate.Value);
            if (toDate.HasValue) { var toExclusive = toDate.Value.AddDays(1); query = query.Where(s => s.CreatedAt < toExclusive); }

            TotalCount = await query.CountAsync();
            TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
            PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

            Slips = await query
                .OrderByDescending(s => s.Status == ExportSlipStatus.Processing)
                .ThenByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.SlipId)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            return Page();
        }

        private static DateTime? ParseDate(string? value) =>
            DateTime.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d) ? d.Date : null;
    }
}
