using System.ComponentModel.DataAnnotations;
using BATT_PPMS.Data;
using BATT_PPMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Auth
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IZaloNotificationService _zaloService;
        private readonly IOtpService _otpService;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            AppDbContext context,
            IZaloNotificationService zaloService,
            IOtpService otpService,
            ILogger<ForgotPasswordModel> logger)
        {
            _context = context;
            _zaloService = zaloService;
            _otpService = otpService;
            _logger = logger;
        }

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc số điện thoại Zalo")]
        public string AccountIdentifier { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetInt32(Helpers.SessionKeys.UserId) != null)
                return RedirectToPage("/Dashboard/Index");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var identifier = AccountIdentifier.Trim();

            // Tìm user theo username hoặc số điện thoại
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == identifier || u.Phone == identifier);

            if (user == null)
            {
                ErrorMessage = "Không tìm thấy tài khoản nào khớp với thông tin đã nhập.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(user.Phone))
            {
                ErrorMessage = "Tài khoản này chưa có số điện thoại Zalo trong hồ sơ. Vui lòng liên hệ Quản trị viên hệ thống (Admin).";
                return Page();
            }

            // Tạo mã OTP 6 số gắn với SĐT Zalo của user
            var otp = _otpService.GenerateAndStoreOtp(user.Phone, TimeSpan.FromMinutes(5));

            // Gửi OTP qua Zalo API External Notification Service
            await _zaloService.SendOtpAsync(user.Phone, otp, "Đặt lại mật khẩu");

            _logger.LogInformation("Đã gửi mã OTP đặt lại mật khẩu cho user {Username} qua Zalo {Phone}", user.Username, user.Phone);

            // Chuyển sang trang Reset Password
            return RedirectToPage("/Auth/ResetPassword", new { username = user.Username });
        }
    }
}
