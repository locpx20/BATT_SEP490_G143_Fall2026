using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace BATT_PPMS.Services
{
    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<OtpService> _logger;
        private const string CachePrefix = "BATT_OTP_";

        public OtpService(IMemoryCache cache, ILogger<OtpService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public string GenerateAndStoreOtp(string key, TimeSpan? expiration = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key không được để trống.", nameof(key));

            var cacheKey = CachePrefix + key.Trim().ToLowerInvariant();

            // Sinh mã OTP 6 số ngẫu nhiên an toàn (100000 - 999999)
            var otpNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
            var otpCode = otpNumber.ToString();

            var ttl = expiration ?? TimeSpan.FromMinutes(5);
            _cache.Set(cacheKey, otpCode, ttl);

            _logger.LogInformation("Generated OTP for {Key} (valid for {Minutes} mins)", key, ttl.TotalMinutes);
            return otpCode;
        }

        public bool VerifyOtp(string key, string inputOtp)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(inputOtp))
                return false;

            var cacheKey = CachePrefix + key.Trim().ToLowerInvariant();
            if (_cache.TryGetValue(cacheKey, out string? cachedOtp))
            {
                if (string.Equals(cachedOtp?.Trim(), inputOtp.Trim(), StringComparison.Ordinal))
                {
                    // Mã đúng -> Xoá khỏi cache để ngăn dùng lại
                    _cache.Remove(cacheKey);
                    _logger.LogInformation("OTP verified successfully for {Key}", key);
                    return true;
                }
            }

            _logger.LogWarning("OTP verification failed for {Key}", key);
            return false;
        }

        public string? PeekOtp(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            var cacheKey = CachePrefix + key.Trim().ToLowerInvariant();
            _cache.TryGetValue(cacheKey, out string? cachedOtp);
            return cachedOtp;
        }

        public void ClearOtp(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var cacheKey = CachePrefix + key.Trim().ToLowerInvariant();
            _cache.Remove(cacheKey);
        }
    }
}
