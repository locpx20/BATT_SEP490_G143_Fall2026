using System.ComponentModel.DataAnnotations;
using BATT_PPMS.Data;
using BATT_PPMS.Models;
using BATT_PPMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Auth
{
    public class ResetPasswordModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IZaloNotificationService _zaloService;
        private readonly IOtpService _otpService;
        private readonly ILogger<ResetPasswordModel> _logger;

        public ResetPasswordModel(
            AppDbContext context,
            IZaloNotificationService zaloService,
            IOtpService otpService,
            ILogger<ResetPasswordModel> logger)
        {
            _context = context;
            _zaloService = zaloService;
            _otpService = otpService;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập mã OTP từ Zalo")]
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

        public string? MaskedPhone { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrWhiteSpace(Username))
                return RedirectToPage("/Auth/ForgotPassword");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == Username);
            if (user == null || string.IsNullOrWhiteSpace(user.Phone))
            {
                return RedirectToPage("/Auth/ForgotPassword");
            }

            MaskedPhone = MaskPhoneNumber(user.Phone);
            SuccessMessage = $"Mã OTP đặt lại mật khẩu đã được gửi đến Zalo số {MaskedPhone}. Vui lòng kiểm tra điện thoại của bạn và nhập mã bên dưới.";
            return Page();
        }

        public async Task<IActionResult> OnPostResetPasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(Username))
                return RedirectToPage("/Auth/ForgotPassword");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == Username);
            if (user == null || string.IsNullOrWhiteSpace(user.Phone))
            {
                ErrorMessage = "Không tìm thấy thông tin tài khoản hợp lệ.";
                return Page();
            }

            MaskedPhone = MaskPhoneNumber(user.Phone);

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Kiểm tra mã OTP qua OtpService
            var isValidOtp = _otpService.VerifyOtp(user.Phone, OtpCode.Trim());
            if (!isValidOtp)
            {
                ErrorMessage = "Mã xác thực OTP không đúng hoặc đã hết hiệu lực (5 phút). Vui lòng thử lại.";
                return Page();
            }

            // Cập nhật mật khẩu mới cho User
            user.Password = NewPassword; // TODO: Hashing khi production
            await _context.SaveChangesAsync();

            // Gửi thông báo bảo mật qua Zalo API
            await _zaloService.SendPasswordChangedAsync(user.Phone, user.Username);

            _logger.LogInformation("Người dùng {Username} đã đặt lại mật khẩu thành công qua Zalo OTP", user.Username);

            return RedirectToPage("/Auth/Login", new { reset = "1" });
        }

        public async Task<IActionResult> OnPostResendOtpAsync()
        {
            if (string.IsNullOrWhiteSpace(Username))
                return RedirectToPage("/Auth/ForgotPassword");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == Username);
            if (user == null || string.IsNullOrWhiteSpace(user.Phone))
            {
                ErrorMessage = "Không tìm thấy thông tin tài khoản.";
                return Page();
            }

            MaskedPhone = MaskPhoneNumber(user.Phone);

            var otp = _otpService.GenerateAndStoreOtp(user.Phone, TimeSpan.FromMinutes(5));
            var zaloResult = await _zaloService.SendOtpAsync(user.Phone, otp, "Đặt lại mật khẩu (Gửi lại)");

            if (!zaloResult.Success)
            {
                ErrorMessage = zaloResult.Message;
            }
            else
            {
                SuccessMessage = $"Đã gửi lại mã OTP mới qua Zalo số {MaskedPhone}. Vui lòng kiểm tra ứng dụng Zalo trên điện thoại.";
            }

            return Page();
        }

        private static string MaskPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone) || phone.Length < 7) return phone;
            // 0900000001 -> 090*****01
            return $"{phone.Substring(0, 3)}*****{phone.Substring(phone.Length - 2)}";
        }
    }
}
