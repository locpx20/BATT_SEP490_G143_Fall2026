using System.Security.Cryptography;
using System.Text;

namespace BATT_PPMS.Helpers;

/// <summary>
/// Mã hoá mật khẩu bằng MD5 (chuỗi hex thường, 32 ký tự) trước khi lưu vào cột User.password.
/// </summary>
public static class PasswordHasher
{
    public static string Hash(string plainText) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(plainText))).ToLowerInvariant();

    public static bool Verify(string? plainText, string? storedHash)
    {
        if (plainText == null || string.IsNullOrWhiteSpace(storedHash)) return false;
        var expected = Encoding.ASCII.GetBytes(Hash(plainText));
        var actual = Encoding.ASCII.GetBytes(storedHash.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
