using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Admin.Notifications;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel : PageModel
{
    private readonly ProductionManagementDbContext _context;

    public IndexModel(ProductionManagementDbContext context) => _context = context;

    public async Task<IActionResult> OnGetAsync()
    {
        if (HttpContext.Session.GetInt32(SessionKeys.UserId) is not > 0)
            return Unauthorized();

        var role = HttpContext.Session.GetString(SessionKeys.RoleName);
        if (role != RoleConstants.Admin && role != RoleConstants.AdminShort)
            return StatusCode(StatusCodes.Status403Forbidden);

        // Current account alerts, not fabricated historical events or timestamps.
        var accounts = await _context.Users.AsNoTracking()
            .Where(u => u.Status != StatusConstants.Active)
            .OrderBy(u => u.Username)
            .Select(u => new { u.Username, u.FullName })
            .Take(10)
            .ToListAsync(HttpContext.RequestAborted);

        return new JsonResult(new
        {
            items = accounts.Select(u => new
            {
                title = "Tài khoản đang tạm khóa",
                message = $"{u.FullName} ({u.Username}) hiện không có quyền truy cập hệ thống.",
                url = Url.Page("/Admin/Users/Index", new { SearchTerm = u.Username })
            })
        });
    }
}
