using BATT_PPMS.Helpers;
using Microsoft.AspNetCore.Mvc;
namespace BATT_PPMS.Pages.ShiftReports;

public class IndexModel : BasePageModel
{
    public IActionResult OnGet() {
        var check = RequireRole(RoleConstants.Staff, RoleConstants.Manager, RoleConstants.Owner);
        return check ?? RedirectToPage("/Warehouse/Consumption");
    }
}
