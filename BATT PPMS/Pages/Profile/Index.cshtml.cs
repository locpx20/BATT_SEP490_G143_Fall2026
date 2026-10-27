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

        [TempData]
        public string? ErrorMessage { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

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

            var error = InputValidator.First(
                InputValidator.Text(FullName, "Họ và tên", 100, required: true),
                InputValidator.Phone(Phone),
                InputValidator.Email(Email));

            var cleanEmail = InputValidator.Clean(Email);
            if (error == null && cleanEmail != null &&
                await _context.Users.AnyAsync(u => u.UserId != CurrentUserId && u.Email != null && u.Email.ToLower() == cleanEmail.ToLower()))
                error = $"Email '{cleanEmail}' đã được liên kết với một tài khoản khác.";

            if (error != null)
            {
                ErrorMessage = error;
                return RedirectToPage("/Profile/Index");
            }

            user.FullName = FullName.Trim();
            user.Phone = InputValidator.Clean(Phone);
            user.Email = cleanEmail;
            await _context.SaveChangesAsync();

            // Cập nhật lại session
            HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);
            SuccessMessage = "Cập nhật hồ sơ thành công.";

            return RedirectToPage("/Profile/Index");
        }
    }
}
