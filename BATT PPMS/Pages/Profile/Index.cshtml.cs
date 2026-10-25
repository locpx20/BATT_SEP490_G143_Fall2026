using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Profile
{
    public class IndexModel : BasePageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) { _context = context; }

        public User? User { get; set; }

        [BindProperty]
        public string FullName { get; set; } = string.Empty;
        [BindProperty]
        public string? Phone { get; set; }
        [BindProperty]
        public string? Email { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var check = RequireLogin();
            if (check != null) return check;

            User = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == CurrentUserId);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var check = RequireLogin();
            if (check != null) return check;

            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();

            user.FullName = FullName;
            user.Phone = Phone;
            user.Email = Email;
            await _context.SaveChangesAsync();

            // Cập nhật lại session
            HttpContext.Session.SetString(SessionKeys.FullName, FullName);

            return RedirectToPage("/Profile/Index");
        }
    }
}
