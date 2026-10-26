using BATT_PPMS.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace BATT_PPMS.Pages.BrokenEquipment
{
    public class IndexModel : BasePageModel
    {
        public IActionResult OnGet()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager, RoleConstants.MaintenanceStaff, RoleConstants.Staff);
            if (check != null) return check;

            return RedirectToPage("/Maintenance/Index");
        }
    }
}
