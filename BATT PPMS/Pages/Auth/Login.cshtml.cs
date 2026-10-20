using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Auth
{
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _context;

        public LoginModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            // Nếu đã đăng nhập rồi thì redirect về Dashboard
            if (HttpContext.Session.GetInt32(SessionKeys.UserId) != null)
                return RedirectToPage("/Dashboard/Index");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Vui lòng nhập đầy đủ thông tin đăng nhập.";
                return Page();
            }

            // TODO: Thay bằng hashed password khi triển khai thực tế
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == Username
                                       && u.Password == Password
                                       && u.Status == StatusConstants.Active);

            if (user == null)
            {
                ErrorMessage = "Tên đăng nhập hoặc mật khẩu không đúng.";
                return Page();
            }

            // Lưu thông tin vào Session
            HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
            HttpContext.Session.SetString(SessionKeys.Username, user.Username);
            HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);
            HttpContext.Session.SetInt32(SessionKeys.RoleId, user.RoleId);
            HttpContext.Session.SetString(SessionKeys.RoleName, user.Role?.RoleName ?? string.Empty);

            return RedirectToPage("/Dashboard/Index");
        }
    }
}
