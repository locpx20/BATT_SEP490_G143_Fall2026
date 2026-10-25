using System.ComponentModel.DataAnnotations;
using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using BATT_PPMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Auth
{
    public class RegisterModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IOtpService _otpService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            AppDbContext context,
            IEmailService emailService,
            IOtpService otpService,
            IConfiguration configuration,
            ILogger<RegisterModel> logger)
        {
            _context = context;
            _emailService = emailService;
            _otpService = otpService;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty]
        public RegisterInput Input { get; set; } = new();

        [BindProperty]
        public string? OtpCode { get; set; }

        [BindProperty]
        public int Step { get; set; } = 1;

        public List<Role> Roles { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        private bool IsMockMode()
        {
            var isMock = _configuration.GetValue<bool>("EmailSettings:EnableMock", false);
            var pass = _configuration["EmailSettings:SmtpPass"];
            var user = _configuration["EmailSettings:SmtpUser"];
            return isMock || string.IsNullOrWhiteSpace(pass) || pass == "your_app_password" || user == "your_email@gmail.com";
        }

        public class RegisterInput
        {
            [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
            [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3 đến 50 ký tự")]
            public string Username { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
            [StringLength(100, ErrorMessage = "Họ và tên tối đa 100 ký tự")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng chọn vai trò (Role)")]
            public int RoleId { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập địa chỉ email")]
            [EmailAddress(ErrorMessage = "Địa chỉ email không đúng định dạng")]
            [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
            [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
            [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetInt32(SessionKeys.UserId) != null)
                return RedirectToPage("/Dashboard/Index");

            await LoadRolesAsync();
            return Page();
        }

        /// <summary>Bước 1: Kiểm tra thông tin → Gửi mã OTP qua Email</summary>
        public async Task<IActionResult> OnPostSendOtpAsync()
        {
            await LoadRolesAsync();

            if (!ModelState.IsValid)
            {
                Step = 1;
                return Page();
            }

            // Kiểm tra trùng username
            var normalizedUsername = Input.Username.Trim().ToLowerInvariant();
            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername))
            {
                ErrorMessage = $"Tên đăng nhập '{Input.Username}' đã tồn tại. Vui lòng chọn tên khác.";
                Step = 1;
                return Page();
            }

            // Kiểm tra trùng email
            var normalizedEmail = Input.Email.Trim().ToLowerInvariant();
            if (await _context.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail))
            {
                ErrorMessage = $"Email '{Input.Email}' đã được đăng ký cho một tài khoản khác.";
                Step = 1;
                return Page();
            }

            // Kiểm tra role hợp lệ
            if (!await _context.Roles.AnyAsync(r => r.RoleId == Input.RoleId))
            {
                ErrorMessage = "Vai trò đã chọn không hợp lệ.";
                Step = 1;
                return Page();
            }

            // Tạo và gửi OTP qua Email
            var otp = _otpService.GenerateAndStoreOtp(normalizedEmail, TimeSpan.FromMinutes(5));
            var sent = await _emailService.SendOtpAsync(Input.Email, otp, "Xác thực đăng ký tài khoản");

            if (!sent)
            {
                ErrorMessage = "Không thể gửi email xác thực. Vui lòng kiểm tra địa chỉ email và thử lại.";
                Step = 1;
                return Page();
            }

            Step = 2;
            if (IsMockMode())
            {
                SuccessMessage = $"[Chế độ TEST/MOCK] Mã OTP của bạn là: {otp} (Cấu hình gửi email thật trong appsettings.json nếu cần gửi về hộp thư thực tế).";
            }
            else
            {
                SuccessMessage = $"Mã OTP đã được gửi đến email {Input.Email}. Vui lòng kiểm tra hộp thư đến (và thư mục Spam).";
            }
            return Page();
        }

        /// <summary>Gửi lại OTP qua Email</summary>
        public async Task<IActionResult> OnPostResendOtpAsync()
        {
            await LoadRolesAsync();

            if (string.IsNullOrWhiteSpace(Input.Email))
            {
                ErrorMessage = "Không tìm thấy địa chỉ email để gửi lại mã OTP.";
                Step = 1;
                return Page();
            }

            var normalizedEmail = Input.Email.Trim().ToLowerInvariant();
            var otp = _otpService.GenerateAndStoreOtp(normalizedEmail, TimeSpan.FromMinutes(5));
            await _emailService.SendOtpAsync(Input.Email, otp, "Xác thực đăng ký tài khoản (Gửi lại)");

            Step = 2;
            if (IsMockMode())
            {
                SuccessMessage = $"[Chế độ TEST/MOCK] Mã OTP mới của bạn là: {otp}.";
            }
            else
            {
                SuccessMessage = $"Đã gửi lại mã OTP mới đến email {Input.Email}.";
            }
            return Page();
        }

        /// <summary>Bước 2: Xác nhận OTP → Hoàn tất đăng ký</summary>
        public async Task<IActionResult> OnPostVerifyAndRegisterAsync()
        {
            await LoadRolesAsync();

            if (string.IsNullOrWhiteSpace(OtpCode))
            {
                ErrorMessage = "Vui lòng nhập mã OTP 6 chữ số nhận được qua email.";
                Step = 2;
                return Page();
            }

            var normalizedEmail = Input.Email.Trim().ToLowerInvariant();
            if (!_otpService.VerifyOtp(normalizedEmail, OtpCode.Trim()))
            {
                ErrorMessage = "Mã OTP không chính xác hoặc đã hết hạn (5 phút). Vui lòng thử lại.";
                Step = 2;
                return Page();
            }

            var newUser = new User
            {
                Username = Input.Username.Trim(),
                Password = Input.Password,
                FullName = Input.FullName.Trim(),
                Email = Input.Email.Trim(),
                Phone = null,
                RoleId = Input.RoleId,
                Status = StatusConstants.Active
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Gửi email chào mừng
            await _emailService.SendWelcomeAsync(newUser.Email!, newUser.FullName, newUser.Username);

            _logger.LogInformation("Người dùng mới {Username} đã đăng ký với RoleId={RoleId}", newUser.Username, newUser.RoleId);

            return RedirectToPage("/Auth/Login", new { registered = "1" });
        }

        private async Task LoadRolesAsync()
        {
            Roles = await _context.Roles.OrderBy(r => r.RoleId).ToListAsync();
        }
    }
}
