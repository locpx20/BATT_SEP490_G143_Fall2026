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
        public string? SuccessMessage { get; set; }

        public IActionResult OnGet(string? reset, string? msg)
        {
            // Nếu đã đăng nhập rồi thì redirect về Dashboard
            if (HttpContext.Session.GetInt32(SessionKeys.UserId) != null)
                return RedirectToPage("/Dashboard/Index");

            if (reset == "1")
            {
                SuccessMessage = "✅ Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.";
            }
            else if (!string.IsNullOrEmpty(msg))
            {
                SuccessMessage = msg;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.";
                return Page();
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username == Username.Trim());

            if (user == null || user.Password != Password)
            {
                ErrorMessage = "Tên đăng nhập hoặc mật khẩu không chính xác.";
                return Page();
            }

            if (user.Status != StatusConstants.Active)
            {
                ErrorMessage = "Tài khoản của bạn đang bị khóa hoặc chưa được kích hoạt bởi Quản trị viên.";
                return Page();
            }

            // Lưu thông tin vào Session
            HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
            HttpContext.Session.SetString(SessionKeys.Username, user.Username);
            HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);
            HttpContext.Session.SetInt32(SessionKeys.RoleId, user.RoleId);
            HttpContext.Session.SetString(SessionKeys.RoleName, user.Role?.RoleName ?? string.Empty);

            if (InitialPasswordPolicy.RequiresChange(user.Role?.RoleName, user.Password))
                return RedirectToPage("/Auth/FirstLoginPassword");

            return RedirectToPage("/Dashboard/Index");
        }
    }
}
