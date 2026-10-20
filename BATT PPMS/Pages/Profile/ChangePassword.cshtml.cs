using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Profile
{
    public class ChangePasswordModel : BasePageModel
    {
        private readonly AppDbContext _context;
        public ChangePasswordModel(AppDbContext context) { _context = context; }

        [BindProperty]
        public string CurrentPassword { get; set; } = string.Empty;
        [BindProperty]
        public string NewPassword { get; set; } = string.Empty;
        [BindProperty]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public IActionResult OnGet()
        {
            var check = RequireLogin();
            if (check != null) return check;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var check = RequireLogin();
            if (check != null) return check;

            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "Mật khẩu mới và xác nhận không khớp.";
                return Page();
            }
            if (NewPassword.Length < 6)
            {
                ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.";
                return Page();
            }

            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();

            // TODO: Thay bằng hash check khi triển khai thực tế
            if (user.Password != CurrentPassword)
            {
                ErrorMessage = "Mật khẩu hiện tại không đúng.";
                return Page();
            }

            user.Password = NewPassword; // TODO: hash password
            await _context.SaveChangesAsync();
            SuccessMessage = "Đổi mật khẩu thành công!";
            return Page();
        }
    }
}
