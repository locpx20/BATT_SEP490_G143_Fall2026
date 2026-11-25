using System.ComponentModel.DataAnnotations;
using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Auth;

public class ForgotPasswordModel(AppDbContext context, TwilioVerifyService verify) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    public string AccountIdentifier { get; set; } = string.Empty;
    [BindProperty] public string OtpCode { get; set; } = string.Empty;
    [BindProperty] public string NewPassword { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public IActionResult OnGet() => HttpContext.Session.GetInt32(SessionKeys.UserId) != null
        ? RedirectToPage("/Dashboard/Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        HttpContext.Session.Remove("PasswordResetUserId");
        HttpContext.Session.Remove("PasswordResetStarted");
        var phone = PhoneHelper.Normalize(AccountIdentifier);
        if (PhoneHelper.Validate(phone) != null) { ErrorMessage = "Vui lòng nhập số điện thoại hợp lệ."; return Page(); }
        var user = await context.Users.FirstOrDefaultAsync(u => u.Phone == phone && u.Status == "Active");
        if (user == null) { ErrorMessage = "Không tìm thấy tài khoản đang hoạt động với số điện thoại này."; return Page(); }
        var result = await verify.SendAsync(TwilioVerifyService.Phone(phone), HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", HttpContext.RequestAborted);
        if (result != TwilioVerifyService.Result.Success) {
            ErrorMessage = result == TwilioVerifyService.Result.RateLimited ? "Vui lòng chờ trước khi yêu cầu gửi lại mã." : "Không thể gửi mã xác thực. Vui lòng kiểm tra cấu hình dịch vụ hoặc thử lại sau.";
            return Page();
        }
        HttpContext.Session.SetInt32("PasswordResetUserId", user.UserId);
        HttpContext.Session.SetString("PasswordResetStarted", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        SuccessMessage = "Đã gửi mã xác thực đến số điện thoại của bạn.";
        return Page();
    }

    public async Task<IActionResult> OnPostResetAsync()
    {
        var id = HttpContext.Session.GetInt32("PasswordResetUserId");
        var start = HttpContext.Session.GetString("PasswordResetStarted");
        if (id == null || !long.TryParse(start, out var timestamp) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp > 600) {
            ErrorMessage = "Vui lòng gửi mã xác thực trước khi đặt lại mật khẩu.";
            return Page();
        }
        var user = await context.Users.FirstOrDefaultAsync(u => u.UserId == id && u.Status == "Active");
        if (user == null || PhoneHelper.Validate(user.Phone) != null) { ErrorMessage = "Không tìm thấy tài khoản hợp lệ."; return Page(); }
        AccountIdentifier = PhoneHelper.Normalize(user.Phone);
        if (!System.Text.RegularExpressions.Regex.IsMatch(OtpCode ?? "", "^[0-9]{6}$")) { ErrorMessage = "Mã OTP phải gồm 6 chữ số."; return Page(); }
        var passwordError = InputValidator.Password(NewPassword, "Mật khẩu mới");
        if (passwordError != null) { ErrorMessage = passwordError; return Page(); }
        if (NewPassword != ConfirmPassword) { ErrorMessage = "Xác nhận mật khẩu không khớp."; return Page(); }
        var result = await verify.CheckAsync(TwilioVerifyService.Phone(user.Phone!), OtpCode!, HttpContext.RequestAborted);
        if (result != TwilioVerifyService.Result.Success) {
            ErrorMessage = result == TwilioVerifyService.Result.InvalidCode ? "Mã OTP không đúng hoặc đã hết hiệu lực." : "Không thể xác thực mã lúc này. Vui lòng thử lại sau.";
            return Page();
        }
        user.Password = PasswordHasher.Hash(NewPassword);
        await context.SaveChangesAsync();
        HttpContext.Session.Remove("PasswordResetUserId");
        HttpContext.Session.Remove("PasswordResetStarted");
        return RedirectToPage("/Auth/Login", new { reset = "1" });
    }
}
