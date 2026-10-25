using System.Net;
using System.Net.Mail;

namespace BATT_PPMS.Services
{
    /// <summary>
    /// Dịch vụ gửi email qua SMTP (Gmail, Outlook, v.v.)
    /// Cấu hình trong appsettings.json tại mục "EmailSettings"
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendOtpAsync(string toEmail, string otpCode, string purpose)
        {
            var subject = $"[BATT PPMS] Mã xác thực OTP - {purpose}";
            var body = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 480px; margin: 0 auto; background: #f8fafc; padding: 32px 16px;"">
  <div style=""background: #1e293b; border-radius: 12px; padding: 32px; text-align: center;"">
    <h2 style=""color: #f1f5f9; margin: 0 0 8px 0; font-size: 22px;"">🏭 BATT PPMS</h2>
    <p style=""color: #94a3b8; margin: 0; font-size: 13px;"">Production Planning &amp; Management System</p>
  </div>
  <div style=""background: #ffffff; border-radius: 12px; padding: 32px; margin-top: 16px; box-shadow: 0 2px 8px rgba(0,0,0,0.06);"">
    <h3 style=""color: #1e293b; margin: 0 0 8px 0;"">Mã xác thực OTP</h3>
    <p style=""color: #64748b; margin: 0 0 24px 0; font-size: 14px;"">Mục đích: <strong>{purpose}</strong></p>
    <div style=""background: #f1f5f9; border-radius: 10px; padding: 24px; text-align: center; margin-bottom: 24px;"">
      <span style=""font-size: 40px; font-weight: 800; letter-spacing: 10px; color: #3b82f6; font-family: monospace;"">{otpCode}</span>
    </div>
    <p style=""color: #ef4444; font-size: 13px; margin: 0 0 8px 0;"">⏱ Mã có hiệu lực trong <strong>5 phút</strong>. Không chia sẻ mã này cho bất kỳ ai.</p>
    <p style=""color: #94a3b8; font-size: 12px; margin: 0;"">Nếu bạn không thực hiện yêu cầu này, hãy bỏ qua email này.</p>
  </div>
  <p style=""text-align: center; color: #94a3b8; font-size: 11px; margin-top: 16px;"">© {DateTime.Now.Year} BATT PPMS. Hệ thống quản lý sản xuất.</p>
</div>";

            return await SendEmailAsync(toEmail, subject, body);
        }

        public async Task<bool> SendWelcomeAsync(string toEmail, string fullName, string username)
        {
            var subject = "[BATT PPMS] Chào mừng bạn đến với hệ thống!";
            var body = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 480px; margin: 0 auto; background: #f8fafc; padding: 32px 16px;"">
  <div style=""background: #1e293b; border-radius: 12px; padding: 32px; text-align: center;"">
    <h2 style=""color: #f1f5f9; margin: 0 0 8px 0; font-size: 22px;"">🏭 BATT PPMS</h2>
    <p style=""color: #94a3b8; margin: 0; font-size: 13px;"">Production Planning &amp; Management System</p>
  </div>
  <div style=""background: #ffffff; border-radius: 12px; padding: 32px; margin-top: 16px; box-shadow: 0 2px 8px rgba(0,0,0,0.06);"">
    <h3 style=""color: #1e293b;"">🎉 Đăng ký thành công!</h3>
    <p style=""color: #64748b;"">Xin chào <strong>{fullName}</strong>,</p>
    <p style=""color: #64748b;"">Tài khoản của bạn đã được tạo thành công trên hệ thống BATT PPMS.</p>
    <div style=""background: #f1f5f9; border-radius: 8px; padding: 16px; margin: 16px 0;"">
      <p style=""margin: 0; color: #475569; font-size: 14px;"">Tên đăng nhập: <strong style=""color: #3b82f6;"">{username}</strong></p>
    </div>
    <p style=""color: #94a3b8; font-size: 13px;"">Hãy đăng nhập và bắt đầu sử dụng hệ thống ngay hôm nay!</p>
  </div>
</div>";

            return await SendEmailAsync(toEmail, subject, body);
        }

        public async Task<bool> SendPasswordChangedAsync(string toEmail, string username)
        {
            var subject = "[BATT PPMS] Mật khẩu đã được thay đổi";
            var body = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 480px; margin: 0 auto; background: #f8fafc; padding: 32px 16px;"">
  <div style=""background: #1e293b; border-radius: 12px; padding: 32px; text-align: center;"">
    <h2 style=""color: #f1f5f9; margin: 0 0 8px 0; font-size: 22px;"">🏭 BATT PPMS</h2>
  </div>
  <div style=""background: #ffffff; border-radius: 12px; padding: 32px; margin-top: 16px; box-shadow: 0 2px 8px rgba(0,0,0,0.06);"">
    <h3 style=""color: #1e293b;"">🔐 Thay đổi mật khẩu thành công</h3>
    <p style=""color: #64748b;"">Tài khoản <strong>{username}</strong> đã đổi mật khẩu thành công lúc <strong>{DateTime.Now:dd/MM/yyyy HH:mm}</strong>.</p>
    <p style=""color: #ef4444; font-size: 13px;"">Nếu bạn không thực hiện hành động này, hãy liên hệ quản trị viên ngay lập tức.</p>
  </div>
</div>";

            return await SendEmailAsync(toEmail, subject, body);
        }

        // ────────────────────────────────────────────────────────────────────────
        private async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var cfg = _configuration.GetSection("EmailSettings");
            var smtpHost = cfg["SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = cfg.GetValue<int>("SmtpPort", 587);
            var smtpUser = cfg["SmtpUser"]?.Trim();
            var smtpPass = cfg["SmtpPass"]?.Replace(" ", "").Trim();
            var fromName = cfg["FromName"] ?? "BATT PPMS";
            var enableSsl = cfg.GetValue<bool>("EnableSsl", true);

            // Chế độ Mock: chỉ ghi log, không gửi thật
            var enableMock = cfg.GetValue<bool>("EnableMock", false);
            if (enableMock || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
            {
                _logger.LogInformation("=================================================");
                _logger.LogInformation("[EmailService - MOCK] To: {To}", toEmail);
                _logger.LogInformation("[EmailService - MOCK] Subject: {Subject}", subject);
                _logger.LogInformation("[EmailService - MOCK] Body (HTML): {Body}", htmlBody);
                _logger.LogInformation("=================================================");
                return true;
            }

            try
            {
                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = enableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 15000
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(smtpUser, fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(toEmail);

                await client.SendMailAsync(message);
                _logger.LogInformation("[EmailService] Sent email to {To} | Subject: {Subject}", toEmail, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EmailService] Failed to send email to {To}", toEmail);
                return false;
            }
        }
    }
}
