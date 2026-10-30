using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Auth;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class FirstLoginPasswordModel(ProductionManagementDbContext database) : PageModel
{
    [BindProperty] public string NewPassword { get; set; } = "";
    [BindProperty] public string ConfirmPassword { get; set; } = "";
    public string? ErrorMessage { get; set; }

    private async Task<User?> GetAccountAsync()
    {
        var id = HttpContext.Session.GetInt32(SessionKeys.UserId);
        return id is > 0 ? await database.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == id) : null;
    }

    private IActionResult? CheckAccount(User? user)
    {
        if (user == null || user.Status != StatusConstants.Active)
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Auth/Login");
        }
        return InitialPasswordPolicy.RequiresChange(user.Role?.RoleName, user.Password)
            ? null : RedirectToPage("/Dashboard/Index");
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await GetAccountAsync();
        return CheckAccount(user) ?? Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await GetAccountAsync();
        var check = CheckAccount(user);
        if (check != null) return check;

        ErrorMessage = InitialPasswordPolicy.ValidateNewPassword(NewPassword, ConfirmPassword);
        if (ErrorMessage != null) return Page();

        user!.Password = NewPassword;
        await database.SaveChangesAsync();
        return RedirectToPage("/Dashboard/Index");
    }
}
