using BATT_PPMS.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace BATT_PPMS.Pages.Customers
{
    public class IndexModel : BasePageModel
    {
        public IActionResult OnGet()
        {
            var check = RequireRole(RoleConstants.Owner, RoleConstants.Manager);
            if (check != null) return check;

            return RedirectToPage("/Suppliers/Index", new { tab = "customers" });
        }
    }
}
