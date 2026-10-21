namespace BATT_PPMS.Services
{
    /// <summary>
    /// Kết quả gửi thông báo qua Zalo API External Notification Service
    /// </summary>
    public class ZaloNotificationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? RecipientPhone { get; set; }
        public string? OtpCode { get; set; }
        public string? Purpose { get; set; }
        public DateTime SentAt { get; set; } = DateTime.Now;
        public string? ZaloMessageId { get; set; }
        public string? PayloadPreview { get; set; }
    }

    /// <summary>
    /// Interface giao tiếp với Zalo API External Notification Service
    /// Theo đúng Usecase Diagram hệ thống Quản lý Sản xuất BATT PPMS
    /// </summary>
    public interface IZaloNotificationService
    {
        /// <summary>
        /// Gửi mã xác thực OTP qua Zalo (cho Đăng ký tài khoản, Quên/Đặt lại mật khẩu)
        /// </summary>
        Task<ZaloNotificationResult> SendOtpAsync(string phoneNumber, string otpCode, string purpose);

        /// <summary>
        /// Gửi thông báo chào mừng khi đăng ký tài khoản thành công qua Zalo
        /// </summary>
        Task<ZaloNotificationResult> SendWelcomeAsync(string phoneNumber, string fullName, string username);

        /// <summary>
        /// Gửi thông báo bảo mật khi tài khoản vừa đổi mật khẩu thành công
        /// </summary>
        Task<ZaloNotificationResult> SendPasswordChangedAsync(string phoneNumber, string username);

        /// <summary>
        /// Lấy thông báo Zalo mới nhất (hỗ trợ hiển thị demo / mô phỏng trên giao diện)
        /// </summary>
        ZaloNotificationResult? GetLastNotification();

        /// <summary>
        /// Xóa thông báo demo sau khi đã hiển thị
        /// </summary>
        void ClearLastNotification();
    }
}
