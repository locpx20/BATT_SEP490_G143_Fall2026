using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Inventory;
public class IndexModel(ProductionManagementDbContext db) : BasePageModel
{
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "raw";
    [BindProperty(SupportsGet = true)] public string? SearchTerm { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public List<RawMaterial> Materials { get; set; } = new();
    public List<FinishedGoodBatch> Batches { get; set; } = new();
    public PageWindow Pagination { get; set; } = new(0, 1);
    public async Task<IActionResult> OnGetAsync() {
        var check = RequireRole(RoleConstants.WarehouseStaff, RoleConstants.Owner, RoleConstants.Manager);
        if (check != null) return check;
        if (Tab != "finished") Tab = "raw";
        var page = PageWindow.ReadRequestedPage(Request.Query, PageNumber);
        if (Tab == "finished") {
            var query = db.FinishedGoodBatches.AsNoTracking().Include(x => x.CreatedByNavigation).Where(x => x.Status == "In Stock");
            if (!string.IsNullOrWhiteSpace(SearchTerm)) query = query.Where(x => x.BagNumber.Contains(SearchTerm.Trim()));
            Pagination = new(await query.CountAsync(), page);
            Batches = await query.OrderByDescending(x => x.ProductionDate).ThenByDescending(x => x.BatchId).Skip(Pagination.Offset).Take(PageWindow.Size).ToListAsync();
        } else {
            var query = db.RawMaterials.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(SearchTerm)) query = query.Where(x => x.MaterialName.Contains(SearchTerm.Trim()));
            Pagination = new(await query.CountAsync(), page);
            Materials = await query.OrderBy(x => x.MaterialName).Skip(Pagination.Offset).Take(PageWindow.Size).ToListAsync();
        }
        return Page();
    }
}
