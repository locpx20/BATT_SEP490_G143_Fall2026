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
        private readonly IEmailService _emailService;
        private readonly IOtpService _otpService;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            AppDbContext context,
            IEmailService emailService,
            IOtpService otpService,
            ILogger<ForgotPasswordModel> logger)
        {
            _context = context;
            _emailService = emailService;
            _otpService = otpService;
            _logger = logger;
        }

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc địa chỉ email")]
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

            // Tìm user theo username hoặc email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == identifier
                    || (u.Email != null && u.Email.ToLower() == identifier.ToLower()));

            if (user == null)
            {
                ErrorMessage = "Không tìm thấy tài khoản nào khớp với thông tin đã nhập.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                ErrorMessage = "Tài khoản này chưa có địa chỉ email. Vui lòng liên hệ Quản trị viên hệ thống.";
                return Page();
            }

            // Tạo mã OTP và gửi qua email
            var otp = _otpService.GenerateAndStoreOtp(user.Email.ToLower(), TimeSpan.FromMinutes(5));
            await _emailService.SendOtpAsync(user.Email, otp, "Đặt lại mật khẩu");

            _logger.LogInformation("Đã gửi OTP đặt lại mật khẩu cho user {Username} qua email {Email}", user.Username, user.Email);

            return RedirectToPage("/Auth/ResetPassword", new { username = user.Username });
        }
    }
}
