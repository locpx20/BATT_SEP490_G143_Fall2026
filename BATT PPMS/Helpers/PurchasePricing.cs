using System.Globalization;

namespace BATT_PPMS.Helpers;

/// <summary>Quy tắc và tiện ích dùng chung cho giá thu mua nguyên liệu.</summary>
public static class PurchasePricing
{
    /// <summary>Đơn giá tối đa (đ/đơn vị).</summary>
    public const long MaxUnitPrice = 100_000_000;

    /// <summary>Số lượng tối đa cho một đơn mua.</summary>
    public const int MaxOrderQuantity = 10_000_000;

    /// <summary>Giới hạn cột total_amount DECIMAL(12,2).</summary>
    public const decimal MaxAmount = 9_999_999_999.99m;

    /// <summary>Định dạng giá không phụ thuộc culture (dùng cho hidden field / data-attribute).</summary>
    public static string FormatInvariant(decimal? price) =>
        price.HasValue ? price.Value.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>
    /// So giá người dùng đang nhìn thấy (chuỗi gửi lên từ form) với giá hiện hành trong DB,
    /// để phát hiện giá vừa bị người khác đổi. Chuỗi rỗng nghĩa là "chưa có giá".
    /// </summary>
    public static bool MatchesCurrent(string? shown, decimal? current)
    {
        if (string.IsNullOrWhiteSpace(shown)) return current == null;
        return decimal.TryParse(shown, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
               && current == value;
    }
}
