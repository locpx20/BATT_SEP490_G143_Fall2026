using System.Text.RegularExpressions;

namespace BATT_PPMS.Helpers;

/// <summary>
/// Số điện thoại là định danh đăng nhập: luôn chuẩn hoá về dạng 0xxxxxxxxx trước khi lưu / so khớp.
/// </summary>
public static class PhoneHelper
{
    private static readonly Regex LoginPhonePattern = new(@"^0\d{9,10}$", RegexOptions.Compiled);

    /// <summary>Bỏ khoảng trắng, dấu chấm, gạch, ngoặc; đổi +84 / 0084 thành 0.</summary>
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var s = new string(phone.Where(c => char.IsAsciiDigit(c) || c == '+').ToArray());
        if (s.StartsWith("+84")) s = "0" + s[3..];
        else if (s.StartsWith("0084")) s = "0" + s[4..];
        return s;
    }

    /// <summary>Kiểm tra SĐT dùng để đăng nhập (bắt buộc, bắt đầu bằng 0, 10–11 chữ số).</summary>
    public static string? Validate(string? phone, string label = "Số điện thoại")
    {
        var normalized = Normalize(phone);
        if (normalized.Length == 0) return $"{label} là bắt buộc.";
        return LoginPhonePattern.IsMatch(normalized)
            ? null
            : $"{label} không hợp lệ (bắt đầu bằng 0, gồm 10–11 chữ số; có thể nhập dạng +84...).";
    }
}
