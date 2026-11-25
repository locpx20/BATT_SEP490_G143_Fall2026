using System.ComponentModel.DataAnnotations;
using BATT_PPMS.Data;
using BATT_PPMS.Helpers;
using BATT_PPMS.Models;
using BATT_PPMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BATT_PPMS.Pages.Auth;

public class ResetPasswordModel(AppDbContext context, TwilioVerifyService verify) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Vui lòng nhập mã OTP"), RegularExpression("^[0-9]{6}$", ErrorMessage = "Mã OTP gồm 6 chữ số")]
    public string OtpCode { get; set; } = string.Empty;
    [BindProperty, Required(ErrorMessage = "Vui lòng nhập mật khẩu mới"), StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; } = string.Empty;
    [BindProperty, Required(ErrorMessage = "Vui lòng xác nhận mật khẩu"), Compare("NewPassword", ErrorMessage = "Xác nhận mật khẩu không khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? MaskedPhone { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    private async Task<User?> CurrentResetUser()
    {
        var id = HttpContext.Session.GetInt32("PasswordResetUserId");
        var start = HttpContext.Session.GetString("PasswordResetStarted");
        if (id == null || !long.TryParse(start, out var timestamp) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp > 600)
            return null;
        var user = await context.Users.FirstOrDefaultAsync(u => u.UserId == id && u.Status == "Active");
        if (user == null || PhoneHelper.Validate(user.Phone) != null) return null;
        var phone = PhoneHelper.Normalize(user.Phone);
        MaskedPhone = phone[..3] + new string('*', phone.Length - 6) + phone[^3..];
        return user;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await CurrentResetUser() == null) return RedirectToPage("/Auth/ForgotPassword");
        SuccessMessage = $"Mã xác thực đã được gửi đến {MaskedPhone}.";
        return Page();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync()
    {
        var user = await CurrentResetUser();
        if (user == null) return RedirectToPage("/Auth/ForgotPassword");
        if (!ModelState.IsValid) return Page();
        var error = InputValidator.Password(NewPassword, "Mật khẩu mới");
        if (error != null) { ErrorMessage = error; return Page(); }
        var result = await verify.CheckAsync(TwilioVerifyService.Phone(user.Phone!), OtpCode.Trim(), HttpContext.RequestAborted);
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

    public async Task<IActionResult> OnPostResendOtpAsync()
    {
        var user = await CurrentResetUser();
        if (user == null) return RedirectToPage("/Auth/ForgotPassword");
        var result = await verify.SendAsync(TwilioVerifyService.Phone(user.Phone!), HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", HttpContext.RequestAborted);
        if (result == TwilioVerifyService.Result.Success) {
            HttpContext.Session.SetString("PasswordResetStarted", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            SuccessMessage = $"Đã gửi mã xác thực đến {MaskedPhone}.";
        } else ErrorMessage = result == TwilioVerifyService.Result.RateLimited ? "Vui lòng chờ trước khi yêu cầu gửi lại mã." : "Không thể gửi lại mã. Vui lòng thử lại sau.";
        return Page();
    }
}
