using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace BATT_PPMS.Pages.Admin.Users
{
    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) { _context = context; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireRole(RoleConstants.Admin);
            if (check != null) return check;
            // TODO: Load users
            return Page();
        }
    }
}
