using System.ComponentModel.DataAnnotations;
using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Auth
{
    public class ResetPasswordModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IOtpService _otpService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ResetPasswordModel> _logger;

        public ResetPasswordModel(
            AppDbContext context,
            IEmailService emailService,
            IOtpService otpService,
            IConfiguration configuration,
            ILogger<ResetPasswordModel> logger)
        {
            _context = context;
            _emailService = emailService;
            _otpService = otpService;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập mã OTP từ email")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP gồm đúng 6 chữ số")]
        public string OtpCode { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự")]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
        [Compare("NewPassword", ErrorMessage = "Xác nhận mật khẩu không khớp")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? MaskedEmail { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        private bool IsMockMode()
        {
            var isMock = _configuration.GetValue<bool>("EmailSettings:EnableMock", false);
            var pass = _configuration["EmailSettings:SmtpPass"];
            var user = _configuration["EmailSettings:SmtpUser"];
            return isMock || string.IsNullOrWhiteSpace(pass) || pass == "your_app_password" || user == "your_email@gmail.com";
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrWhiteSpace(Username))
                return RedirectToPage("/Auth/ForgotPassword");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == Username);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
                return RedirectToPage("/Auth/ForgotPassword");

            MaskedEmail = MaskEmail(user.Email);
            if (IsMockMode())
            {
                var mockOtp = _otpService.PeekOtp(user.Email.ToLower());
                SuccessMessage = $"[Chế độ TEST/MOCK] Mã OTP của bạn là: {mockOtp} (Cấu hình gửi email thật trong appsettings.json nếu cần gửi về hộp thư thực tế).";
            }
            else
            {
                SuccessMessage = $"Mã OTP đặt lại mật khẩu đã được gửi đến {MaskedEmail}. Vui lòng kiểm tra hộp thư email (và thư mục Spam).";
            }
            return Page();
        }

        public async Task<IActionResult> OnPostResetPasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(Username))
                return RedirectToPage("/Auth/ForgotPassword");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == Username);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                ErrorMessage = "Không tìm thấy thông tin tài khoản hợp lệ.";
                return Page();
            }

            MaskedEmail = MaskEmail(user.Email);

            if (!ModelState.IsValid)
                return Page();

            var pwError = InputValidator.Password(NewPassword, "Mật khẩu mới");
            if (pwError != null)
            {
                ErrorMessage = pwError;
                return Page();
            }

            if (!_otpService.VerifyOtp(user.Email.ToLower(), OtpCode.Trim()))
            {
                ErrorMessage = "Mã OTP không đúng hoặc đã hết hiệu lực (5 phút). Vui lòng thử lại.";
                return Page();
            }

            user.Password = NewPassword;
            await _context.SaveChangesAsync();

            await _emailService.SendPasswordChangedAsync(user.Email, user.Username);

            _logger.LogInformation("Người dùng {Username} đã đặt lại mật khẩu thành công", user.Username);

            return RedirectToPage("/Auth/Login", new { reset = "1" });
        }

        public async Task<IActionResult> OnPostResendOtpAsync()
        {
            if (string.IsNullOrWhiteSpace(Username))
                return RedirectToPage("/Auth/ForgotPassword");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == Username);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                ErrorMessage = "Không tìm thấy thông tin tài khoản.";
                return Page();
            }

            MaskedEmail = MaskEmail(user.Email);

            var otp = _otpService.GenerateAndStoreOtp(user.Email.ToLower(), TimeSpan.FromMinutes(5));
            var sent = await _emailService.SendOtpAsync(user.Email, otp, "Đặt lại mật khẩu (Gửi lại)");

            if (sent)
            {
                if (IsMockMode())
                    SuccessMessage = $"[Chế độ TEST/MOCK] Mã OTP mới của bạn là: {otp}.";
                else
                    SuccessMessage = $"Đã gửi lại mã OTP mới đến {MaskedEmail}. Vui lòng kiểm tra hộp thư email.";
            }
            else
                ErrorMessage = "Không thể gửi lại email. Vui lòng thử lại sau.";

            return Page();
        }

        private static string MaskEmail(string email)
        {
            var at = email.IndexOf('@');
            if (at <= 1) return email;
            return email[0] + new string('*', Math.Min(at - 1, 4)) + email[at..];
        }
    }
}
