using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using BATT_PPMS.Helpers;

namespace BATT_PPMS.Services;

public sealed class TwilioVerifyService(HttpClient client, IConfiguration configuration, IMemoryCache cache, ILogger<TwilioVerifyService> logger)
{
    private static readonly object RateLock = new();
    public enum Result { Success, InvalidCode, Unavailable, RateLimited }

    public static string Phone(string phone) => "+84" + PhoneHelper.Normalize(phone)[1..];

    public async Task<Result> SendAsync(string phone, string clientAddress, CancellationToken cancellationToken)
    {
        lock (RateLock)
        {
            var phoneKey = "verify-send-phone:" + phone;
            var ipKey = "verify-send-ip:" + clientAddress;
            if (cache.TryGetValue(phoneKey, out _) || cache.Get<int>(ipKey) >= 10)
                return Result.RateLimited;
            cache.Set(phoneKey, true, TimeSpan.FromSeconds(60));
            cache.Set(ipKey, cache.Get<int>(ipKey) + 1, TimeSpan.FromMinutes(10));
        }
        return await PostAsync("Verifications", new() { ["To"] = phone, ["Channel"] = "sms" }, "pending", cancellationToken);
    }

    public Task<Result> CheckAsync(string phone, string code, CancellationToken cancellationToken) =>
        PostAsync("VerificationCheck", new() { ["To"] = phone, ["Code"] = code }, "approved", cancellationToken);

    private async Task<Result> PostAsync(string endpoint, Dictionary<string, string> fields, string expected, CancellationToken cancellationToken)
    {
        var sid = configuration["Twilio:AccountSid"];
        var token = configuration["Twilio:AuthToken"];
        var service = configuration["Twilio:VerifyServiceSid"];
        if (string.IsNullOrWhiteSpace(sid) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(service)
            || !System.Text.RegularExpressions.Regex.IsMatch(service, "^VA[0-9a-fA-F]{32}$"))
            return Result.Unavailable;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://verify.twilio.com/v2/Services/{service}/{endpoint}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sid}:{token}")));
        request.Content = new FormUrlEncodedContent(fields);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (endpoint == "VerificationCheck" && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return Result.InvalidCode;
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Twilio Verify rejected request with HTTP status {Status}", (int)response.StatusCode);
                return Result.Unavailable;
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return json.RootElement.TryGetProperty("status", out var status) && status.GetString() == expected
                ? Result.Success : endpoint == "VerificationCheck" ? Result.InvalidCode : Result.Unavailable;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning("Twilio Verify request could not be completed");
            return Result.Unavailable;
        }
    }
}
