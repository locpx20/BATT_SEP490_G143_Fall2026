using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.PurchasePrices
{
    public class MaterialPriceRow
    {
        public int MaterialId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public DateTime? LastChangedAt { get; set; }
        public string? LastChangedBy { get; set; }
    }

    public class PriceHistoryRow
    {
        public DateTime ChangedAt { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal? OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public string ChangedByName { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true, Name = "material")]
        public int? FilterMaterialId { get; set; }

        public List<MaterialPriceRow> Materials { get; set; } = new();
        public List<PriceHistoryRow> History { get; set; } = new();
        public PageWindow Pagination { get; private set; } = new(0, 1);

        // Chỉ Owner / Manager được thiết lập giá; các vai trò thu mua chỉ xem.
        public bool CanManage => IsOwnerOrManager;

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager, RoleConstants.PurchasingStaff);
            if (check != null) return check;

            PageNumber = PageWindow.ReadRequestedPage(Request.Query, PageNumber);
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdatePriceAsync(int priceMaterialId, long newPrice, string? expectedOldPrice, string? reason)
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            if (newPrice < 1 || newPrice > PurchasePricing.MaxUnitPrice)
            {
                ErrorMessage = $"Đơn giá thu mua phải là số nguyên từ 1 đến {PurchasePricing.MaxUnitPrice:N0} đồng.";
                return RedirectToPage();
            }

            var reasonError = InputValidator.Text(reason, "Lý do thay đổi", 500);
            if (reasonError != null)
            {
                ErrorMessage = reasonError;
                return RedirectToPage();
            }

            var material = await _context.RawMaterials.FirstOrDefaultAsync(m => m.MaterialId == priceMaterialId);
            if (material == null)
            {
                ErrorMessage = "Không tìm thấy nguyên liệu cần cập nhật giá.";
                return RedirectToPage();
            }

            // Giá người dùng đang nhìn thấy phải còn khớp DB, tránh ghi đè thay đổi của người khác.
            if (!PurchasePricing.MatchesCurrent(expectedOldPrice, material.PurchasePrice))
            {
                ErrorMessage = $"Giá thu mua của '{material.MaterialName}' vừa được người khác thay đổi. Vui lòng kiểm tra giá hiện hành rồi cập nhật lại.";
                return RedirectToPage();
            }

            if (material.PurchasePrice == newPrice)
            {
                ErrorMessage = "Giá mới trùng với giá hiện hành nên không có gì để thay đổi.";
                return RedirectToPage();
            }

            // Ghi lịch sử và cập nhật giá hiện hành trong cùng một lần SaveChanges (một transaction)
            _context.RawMaterialPriceHistories.Add(new RawMaterialPriceHistory
            {
                MaterialId = material.MaterialId,
                OldPrice = material.PurchasePrice,
                NewPrice = newPrice,
                ChangedBy = CurrentUserId,
                ChangedAt = DateTime.Now,
                Reason = InputValidator.Clean(reason)
            });
            var oldPrice = material.PurchasePrice;
            material.PurchasePrice = newPrice;
            await _context.SaveChangesAsync();

            SuccessMessage = oldPrice == null
                ? $"Đã thiết lập giá thu mua '{material.MaterialName}': {newPrice:N0} đ/{material.Unit}. Giá này sẽ được dùng cho mọi đơn mua mới cho đến khi thay đổi."
                : $"Đã đổi giá thu mua '{material.MaterialName}' từ {oldPrice.Value:N0} thành {newPrice:N0} đ/{material.Unit}. Các đơn mua đã tạo trước đó giữ nguyên giá cũ.";
            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            var materials = await _context.RawMaterials
                .AsNoTracking()
                .OrderBy(m => m.MaterialName)
                .ToListAsync();

            foreach (var m in materials)
            {
                var last = await _context.RawMaterialPriceHistories
                    .AsNoTracking()
                    .Where(h => h.MaterialId == m.MaterialId)
                    .OrderByDescending(h => h.ChangedAt)
                    .ThenByDescending(h => h.HistoryId)
                    .Select(h => new { h.ChangedAt, Name = h.ChangedByNavigation == null ? "Tài khoản đã xóa" : h.ChangedByNavigation.FullName })
                    .FirstOrDefaultAsync();

                Materials.Add(new MaterialPriceRow
                {
                    MaterialId = m.MaterialId,
                    Name = m.MaterialName,
                    Unit = m.Unit,
                    Price = m.PurchasePrice,
                    LastChangedAt = last?.ChangedAt,
                    LastChangedBy = last?.Name
                });
            }

            var query = _context.RawMaterialPriceHistories.AsNoTracking().AsQueryable();
            if (FilterMaterialId.HasValue)
                query = query.Where(h => h.MaterialId == FilterMaterialId.Value);

            Pagination = new PageWindow(await query.CountAsync(), PageNumber);

            History = await query
                .OrderByDescending(h => h.ChangedAt)
                .ThenByDescending(h => h.HistoryId)
                .Skip(Pagination.Offset)
                .Take(PageWindow.Size)
                .Select(h => new PriceHistoryRow
                {
                    ChangedAt = h.ChangedAt,
                    MaterialName = h.Material.MaterialName,
                    Unit = h.Material.Unit,
                    OldPrice = h.OldPrice,
                    NewPrice = h.NewPrice,
                    ChangedByName = h.ChangedByNavigation == null ? "Tài khoản đã xóa" : h.ChangedByNavigation.FullName,
                    Reason = h.Reason
                })
                .ToListAsync();
        }
    }
}
