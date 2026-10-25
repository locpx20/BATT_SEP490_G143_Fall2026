namespace BATT_PPMS.Services
{
    public interface IEmailService
    {
        /// <summary>Gửi mã OTP xác thực về địa chỉ email</summary>
        Task<bool> SendOtpAsync(string toEmail, string otpCode, string purpose);

        /// <summary>Gửi email chào mừng sau khi đăng ký thành công</summary>
        Task<bool> SendWelcomeAsync(string toEmail, string fullName, string username);

        /// <summary>Gửi thông báo đổi mật khẩu thành công</summary>
        Task<bool> SendPasswordChangedAsync(string toEmail, string username);
    }
}
