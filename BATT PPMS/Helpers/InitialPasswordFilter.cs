using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Helpers;

public class InitialPasswordFilter(ProductionManagementDbContext database) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var page = context.ActionDescriptor.ViewEnginePath;
        var id = context.HttpContext.Session.GetInt32(SessionKeys.UserId);
        if (id is > 0 && page != "/Auth/FirstLoginPassword" && page != "/Auth/Logout")
        {
            // Read the stored password so an existing session or a reset to the default
            // cannot bypass the first-login requirement by visiting a URL directly.
            var account = await database.Users.AsNoTracking().Where(u => u.UserId == id)
                .Select(u => new { u.Password, Role = u.Role.RoleName })
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);
            if (account != null && InitialPasswordPolicy.RequiresChange(account.Role, account.Password))
            {
                context.Result = new RedirectToPageResult("/Auth/FirstLoginPassword");
                return;
            }
        }
        await next();
    }
}
