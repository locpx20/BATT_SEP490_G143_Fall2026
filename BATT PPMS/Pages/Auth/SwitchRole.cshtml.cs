using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Auth
{
    public class SwitchRoleModel : PageModel
    {
        private readonly AppDbContext _context;

        public SwitchRoleModel(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync(string? role, string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return RedirectToPage("/Dashboard/Index");
            }

            role = role.Trim();

            // Try to find an active user with this role in the DB
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Role != null && (u.Role.RoleName == role || (role == "Admin" && u.Role.RoleName == "Admin System")));

            if (user != null)
            {
                HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
                HttpContext.Session.SetString(SessionKeys.Username, user.Username);
                HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);
                HttpContext.Session.SetInt32(SessionKeys.RoleId, user.RoleId);
                HttpContext.Session.SetString(SessionKeys.RoleName, user.Role?.RoleName ?? role);
            }
            else
            {
                // Fallback mock session for roles not seeded
                var roleObj = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == role || (role == "Admin" && r.RoleName == "Admin System"));
                int roleId = roleObj?.RoleId ?? 7;
                string assignedRole = roleObj?.RoleName ?? role;

                HttpContext.Session.SetInt32(SessionKeys.UserId, 999);
                HttpContext.Session.SetString(SessionKeys.Username, role.ToLower().Replace(" ", "_"));
                HttpContext.Session.SetString(SessionKeys.FullName, $"{assignedRole} Demo");
                HttpContext.Session.SetInt32(SessionKeys.RoleId, roleId);
                HttpContext.Session.SetString(SessionKeys.RoleName, assignedRole);
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (role.Contains("Admin"))
            {
                return RedirectToPage("/Admin/Users/Index");
            }

            return RedirectToPage("/Dashboard/Index");
        }
    }
}
