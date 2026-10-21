namespace BATT_PPMS.Services
{
    public interface IOtpService
    {
        /// <summary>
        /// Tạo và lưu mã OTP 6 số gắn với key (số điện thoại hoặc username), mặc định hết hạn sau 5 phút
        /// </summary>
        string GenerateAndStoreOtp(string key, TimeSpan? expiration = null);

        /// <summary>
        /// Kiểm tra mã OTP người dùng nhập vào. Nếu hợp lệ, tự động xoá để tránh sử dụng lại.
        /// </summary>
        bool VerifyOtp(string key, string inputOtp);

        /// <summary>
        /// Lấy mã OTP hiện tại (dùng để hỗ trợ hiển thị hoặc kiểm thử)
        /// </summary>
        string? PeekOtp(string key);

        /// <summary>
        /// Huỷ bỏ mã OTP đang có của key
        /// </summary>
        void ClearOtp(string key);
    }
}
