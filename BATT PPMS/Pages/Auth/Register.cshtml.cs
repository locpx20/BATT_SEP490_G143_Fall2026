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
        private readonly IZaloNotificationService _zaloService;
        private readonly IOtpService _otpService;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            AppDbContext context,
            IZaloNotificationService zaloService,
            IOtpService otpService,
            ILogger<RegisterModel> logger)
        {
            _context = context;
            _zaloService = zaloService;
            _otpService = otpService;
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

            [Required(ErrorMessage = "Vui lòng nhập số điện thoại Zalo")]
            [RegularExpression(@"^(0[3|5|7|8|9])[0-9]{8}$|^84[3|5|7|8|9][0-9]{8}$", ErrorMessage = "Số điện thoại Zalo không đúng định dạng (VD: 0912345678)")]
            public string Phone { get; set; } = string.Empty;

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

        /// <summary>
        /// Bước 1: Kiểm tra thông tin hợp lệ -> Gửi mã OTP xác thực qua Zalo API
        /// </summary>
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
            var existsUsername = await _context.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername);
            if (existsUsername)
            {
                ErrorMessage = $"Tên đăng nhập '{Input.Username}' đã tồn tại trong hệ thống. Vui lòng chọn tên khác.";
                Step = 1;
                return Page();
            }

            // Kiểm tra trùng số điện thoại
            var normalizedPhone = Input.Phone.Trim();
            var existsPhone = await _context.Users.AnyAsync(u => u.Phone == normalizedPhone);
            if (existsPhone)
            {
                ErrorMessage = $"Số điện thoại {Input.Phone} đã được đăng ký cho một tài khoản khác.";
                Step = 1;
                return Page();
            }

            // Kiểm tra role hợp lệ
            var roleExists = await _context.Roles.AnyAsync(r => r.RoleId == Input.RoleId);
            if (!roleExists)
            {
                ErrorMessage = "Vai trò đã chọn không hợp lệ.";
                Step = 1;
                return Page();
            }

            // Tạo mã OTP 6 số
            var otp = _otpService.GenerateAndStoreOtp(normalizedPhone, TimeSpan.FromMinutes(5));

            // Gọi Zalo API External Notification Service gửi về điện thoại / app Zalo
            var zaloResult = await _zaloService.SendOtpAsync(normalizedPhone, otp, "Xác thực đăng ký tài khoản");

            if (!zaloResult.Success)
            {
                ErrorMessage = zaloResult.Message;
                Step = 1;
                return Page();
            }

            Step = 2;
            SuccessMessage = $"Mã xác thực OTP đã được gửi trực tiếp đến ứng dụng Zalo của số điện thoại {normalizedPhone}. Vui lòng kiểm tra điện thoại và nhập mã xác thực.";
            return Page();
        }

        /// <summary>
        /// Gửi lại mã OTP qua Zalo
        /// </summary>
        public async Task<IActionResult> OnPostResendOtpAsync()
        {
            await LoadRolesAsync();

            if (string.IsNullOrWhiteSpace(Input.Phone))
            {
                ErrorMessage = "Không tìm thấy số điện thoại để gửi lại mã OTP.";
                Step = 1;
                return Page();
            }

            var normalizedPhone = Input.Phone.Trim();
            var otp = _otpService.GenerateAndStoreOtp(normalizedPhone, TimeSpan.FromMinutes(5));
            var zaloResult = await _zaloService.SendOtpAsync(normalizedPhone, otp, "Xác thực đăng ký tài khoản (Gửi lại)");

            Step = 2;
            if (!zaloResult.Success)
            {
                ErrorMessage = zaloResult.Message;
            }
            else
            {
                SuccessMessage = $"Đã gửi lại mã OTP qua Zalo tới số điện thoại {normalizedPhone}. Vui lòng kiểm tra ứng dụng Zalo.";
            }

            return Page();
        }

        /// <summary>
        /// Bước 2: Xác nhận OTP Zalo và Hoàn tất đăng ký người dùng với Role đã chọn
        /// </summary>
        public async Task<IActionResult> OnPostVerifyAndRegisterAsync()
        {
            await LoadRolesAsync();

            if (string.IsNullOrWhiteSpace(OtpCode))
            {
                ErrorMessage = "Vui lòng nhập mã OTP 6 chữ số nhận được từ ứng dụng Zalo trên điện thoại.";
                Step = 2;
                return Page();
            }

            var normalizedPhone = Input.Phone.Trim();
            var isOtpValid = _otpService.VerifyOtp(normalizedPhone, OtpCode.Trim());

            if (!isOtpValid)
            {
                ErrorMessage = "Mã xác thực OTP không chính xác hoặc đã hết hạn (5 phút). Vui lòng thử lại.";
                Step = 2;
                return Page();
            }

            // Tạo người dùng với vai trò người dùng đã chọn
            var newUser = new User
            {
                Username = Input.Username.Trim(),
                Password = Input.Password, // Lưu ý: thực tế cần hash
                FullName = Input.FullName.Trim(),
                Phone = normalizedPhone,
                Email = null, // Đã bỏ nhập email theo yêu cầu
                RoleId = Input.RoleId,
                Status = StatusConstants.Active
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Gửi thông báo chào mừng qua Zalo API tới số điện thoại
            await _zaloService.SendWelcomeAsync(newUser.Phone, newUser.FullName, newUser.Username);

            _logger.LogInformation("Người dùng mới {Username} đã đăng ký với RoleId={RoleId} qua Zalo OTP verification", newUser.Username, newUser.RoleId);

            return RedirectToPage("/Auth/Login", new { registered = "1" });
        }

        private async Task LoadRolesAsync()
        {
            Roles = await _context.Roles.OrderBy(r => r.RoleId).ToListAsync();
        }
    }
}
