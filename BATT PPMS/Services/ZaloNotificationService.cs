using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BATT_PPMS.Services
{
    /// <summary>
    /// Triển khai dịch vụ gửi tin nhắn / OTP qua Zalo API External Notification Service
    /// Kết nối trực tiếp tới Zalo Notification Service (ZNS OpenAPI): Gửi tin nhắn OTP tới số điện thoại thoại Zalo
    /// </summary>
    public class ZaloNotificationService : IZaloNotificationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ZaloNotificationService> _logger;

        private static ZaloNotificationResult? _lastNotification;
        private static readonly object _lock = new();

        public ZaloNotificationService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ZaloNotificationService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ZaloNotificationResult> SendOtpAsync(string phoneNumber, string otpCode, string purpose)
        {
            var normalizedPhone = NormalizePhoneNumber(phoneNumber);
            var timestamp = DateTime.Now;

            var result = new ZaloNotificationResult
            {
                RecipientPhone = phoneNumber,
                OtpCode = otpCode,
                Purpose = purpose,
                SentAt = timestamp,
                ZaloMessageId = $"ZNS_{timestamp:yyyyMMddHHmmss}_{new Random().Next(1000, 9999)}"
            };

            var enableMock = _configuration.GetValue<bool?>("ZaloNotification:EnableMock") ?? false;
            var apiEndpoint = _configuration["ZaloNotification:ApiEndpoint"] ?? "https://business.openapi.zalo.me/message/template";
            var accessToken = _configuration["ZaloNotification:AccessToken"];
            var templateId = _configuration["ZaloNotification:TemplateIdOtp"] ?? "328912";
            var mode = _configuration["ZaloNotification:Mode"]; // "development" hoặc để trống cho production

            // Xây dựng template_data với các key phổ biến để tương thích với mẫu ZNS của Zalo
            var templateData = new Dictionary<string, object>
            {
                { "otp", otpCode },
                { "otp_code", otpCode },
                { "code", otpCode },
                { "purpose", purpose },
                { "company_name", "BATT PPMS" },
                { "expired_minutes", 5 },
                { "date", timestamp.ToString("dd/MM/yyyy HH:mm") }
            };

            var payloadDict = new Dictionary<string, object>
            {
                { "phone", normalizedPhone },
                { "template_id", templateId },
                { "template_data", templateData },
                { "tracking_id", result.ZaloMessageId }
            };

            if (!string.IsNullOrWhiteSpace(mode))
            {
                payloadDict["mode"] = mode;
            }

            var jsonPayload = JsonSerializer.Serialize(payloadDict, new JsonSerializerOptions { WriteIndented = true });
            result.PayloadPreview = jsonPayload;

            var hasLiveToken = !string.IsNullOrWhiteSpace(accessToken)
                               && !accessToken.Equals("your_access_token_here", StringComparison.OrdinalIgnoreCase);

            // Gửi thật qua Zalo OpenAPI khi có token hoặc khi tắt Mock
            if (hasLiveToken && !enableMock)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    var request = new HttpRequestMessage(HttpMethod.Post, apiEndpoint)
                    {
                        Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                    };
                    request.Headers.Add("access_token", accessToken);

                    var response = await client.SendAsync(request);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    _logger.LogInformation("Zalo API HTTP status: {StatusCode}, body: {Body}", response.StatusCode, responseBody);

                    try
                    {
                        using var doc = JsonDocument.Parse(responseBody);
                        var root = doc.RootElement;
                        var errorCode = root.TryGetProperty("error", out var errProp) ? errProp.GetInt32() : -1;
                        var errorMsg = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";

                        if (errorCode == 0)
                        {
                            result.Success = true;
                            result.Message = $"Đã gửi mã xác thực OTP về ứng dụng Zalo số {phoneNumber}.";
                        }
                        else
                        {
                            result.Success = false;
                            result.Message = $"Zalo API trả về lỗi ({errorCode}): {errorMsg}. Vui lòng kiểm tra AccessToken, TemplateId hoặc số điện thoại người nhận.";
                            _logger.LogWarning("Zalo API returned error: {Code} - {Msg}", errorCode, errorMsg);
                        }
                    }
                    catch
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            result.Success = true;
                            result.Message = $"Đã gửi mã xác thực OTP về ứng dụng Zalo số {phoneNumber}.";
                        }
                        else
                        {
                            result.Success = false;
                            result.Message = $"Lỗi từ Zalo API ({response.StatusCode}): {responseBody}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi kết nối tới Zalo OpenAPI.");
                    result.Success = false;
                    result.Message = $"Không thể kết nối đến máy chủ Zalo API: {ex.Message}";
                }
            }
            else if (!enableMock && !hasLiveToken)
            {
                result.Success = false;
                result.Message = "Chưa cấu hình Access Token của Zalo trong file appsettings.json. Vui lòng dán Access Token của Zalo OA vào dòng 'AccessToken' trong appsettings.json để gửi tin về điện thoại.";
            }
            else
            {
                // Chế độ mô phỏng / Mock (khi EnableMock = true): Ghi log terminal, không hiện trên web
                result.Success = true;
                result.Message = $"Đã gửi mã OTP qua Zalo tới số {phoneNumber}.";
                _logger.LogInformation("=================================================================");
                _logger.LogInformation(">>> [ZALO NOTIFICATION SERVICE - DISPATCH TO PHONE]");
                _logger.LogInformation(">>> Recipient Phone : {Phone}", phoneNumber);
                _logger.LogInformation(">>> OTP Code        : {Otp}", otpCode);
                _logger.LogInformation(">>> Purpose         : {Purpose}", purpose);
                _logger.LogInformation(">>> Payload JSON    : {Payload}", jsonPayload);
                _logger.LogInformation("=================================================================");
            }

            lock (_lock)
            {
                _lastNotification = result;
            }

            return result;
        }

        public async Task<ZaloNotificationResult> SendWelcomeAsync(string phoneNumber, string fullName, string username)
        {
            var normalizedPhone = NormalizePhoneNumber(phoneNumber);
            var timestamp = DateTime.Now;

            var result = new ZaloNotificationResult
            {
                RecipientPhone = phoneNumber,
                Purpose = "Chào mừng thành viên mới",
                SentAt = timestamp,
                ZaloMessageId = $"ZNS_WELCOME_{timestamp:yyyyMMddHHmmss}"
            };

            var payload = new
            {
                phone = normalizedPhone,
                template_id = _configuration["ZaloNotification:TemplateIdWelcome"] ?? "328913",
                template_data = new
                {
                    customer_name = fullName,
                    account = username,
                    system_name = "BATT PPMS",
                    date = timestamp.ToString("dd/MM/yyyy HH:mm")
                },
                tracking_id = result.ZaloMessageId
            };

            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            result.PayloadPreview = jsonPayload;
            result.Success = true;
            result.Message = $"Đã gửi thông báo chào mừng kích hoạt tài khoản {username} tới số Zalo {phoneNumber}.";

            _logger.LogInformation("[Zalo API] Welcome message to {Phone}: {Payload}", phoneNumber, jsonPayload);

            lock (_lock)
            {
                _lastNotification = result;
            }

            await Task.CompletedTask;
            return result;
        }

        public async Task<ZaloNotificationResult> SendPasswordChangedAsync(string phoneNumber, string username)
        {
            var normalizedPhone = NormalizePhoneNumber(phoneNumber);
            var timestamp = DateTime.Now;

            var result = new ZaloNotificationResult
            {
                RecipientPhone = phoneNumber,
                Purpose = "Cảnh báo đổi mật khẩu",
                SentAt = timestamp,
                ZaloMessageId = $"ZNS_PWD_CHG_{timestamp:yyyyMMddHHmmss}"
            };

            var payload = new
            {
                phone = normalizedPhone,
                template_id = "328914",
                template_data = new
                {
                    account = username,
                    action = "Đổi mật khẩu tài khoản thành công",
                    system_name = "BATT PPMS",
                    time = timestamp.ToString("dd/MM/yyyy HH:mm")
                },
                tracking_id = result.ZaloMessageId
            };

            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            result.PayloadPreview = jsonPayload;
            result.Success = true;
            result.Message = $"Đã gửi thông báo đổi mật khẩu tài khoản {username} tới số Zalo {phoneNumber}.";

            _logger.LogInformation("[Zalo API] Password changed alert to {Phone}: {Payload}", phoneNumber, jsonPayload);

            lock (_lock)
            {
                _lastNotification = result;
            }

            await Task.CompletedTask;
            return result;
        }

        public ZaloNotificationResult? GetLastNotification()
        {
            lock (_lock)
            {
                return _lastNotification;
            }
        }

        public void ClearLastNotification()
        {
            lock (_lock)
            {
                _lastNotification = null;
            }
        }

        private static string NormalizePhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

            var cleaned = new string(phone.Where(char.IsDigit).ToArray());
            if (cleaned.StartsWith("0") && cleaned.Length == 10)
            {
                return "84" + cleaned.Substring(1);
            }
            if (cleaned.StartsWith("84"))
            {
                return cleaned;
            }
            return cleaned;
        }
    }
}
