using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;

namespace BATT_PPMS.ViewComponents;

public sealed class WarehouseNavigationViewComponent(ProductionManagementDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string activePage) =>
        View("~/Pages/Shared/Roles/Warehouse/_Navigation.cshtml",
            new WarehouseNavigationModel(activePage, await WarehouseInboxSummary.ReadAsync(db,
                HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0, HttpContext.RequestAborted)));
}
