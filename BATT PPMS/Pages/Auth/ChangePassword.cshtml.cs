using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BATT_PPMS.Pages.Auth
{
    public class ChangePasswordModel : PageModel
    {
        public IActionResult OnGet()
        {
            return RedirectToPage("/Profile/ChangePassword");
        }

        public IActionResult OnPost()
        {
            return RedirectToPage("/Profile/ChangePassword");
        }
    }
}
