namespace BATT_PPMS.Helpers;

/// <summary>
/// Chuyển đổi số thành chữ tiếng Việt chuẩn cho chứng từ, hóa đơn, phiếu xuất kho.
/// </summary>
public static class VietnameseNumberReader
{
    private static readonly string[] Digits = { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
    private static readonly string[] Units = { "", "nghìn", "triệu", "tỷ", "nghìn tỷ", "triệu tỷ" };

    public static string ToWords(long number)
    {
        if (number == 0) return "Không";
        if (number < 0) return "Âm " + ToWords(-number).ToLower();

        var sNumber = number.ToString();
        var groups = new List<string>();
        for (var i = sNumber.Length; i > 0; i -= 3)
        {
            var start = Math.Max(0, i - 3);
            var len = i - start;
            groups.Add(sNumber.Substring(start, len));
        }

        var result = new List<string>();
        for (var i = groups.Count - 1; i >= 0; i--)
        {
            var groupVal = int.Parse(groups[i]);
            if (groupVal == 0) continue;

            var groupWords = ReadThreeDigits(groupVal, i < groups.Count - 1);
            result.Add(groupWords + (string.IsNullOrEmpty(Units[i]) ? "" : " " + Units[i]));
        }

        var text = string.Join(" ", result).Trim();
        if (string.IsNullOrEmpty(text)) return "Không";

        // Viết hoa chữ cái đầu tiên
        return char.ToUpper(text[0]) + text[1..];
    }

    public static string ToKgWords(decimal kg)
    {
        var longKg = (long)Math.Round(kg);
        var words = ToWords(longKg);
        return $"{words} kilôgam";
    }

    public static string ToBagsWords(int bags)
    {
        var words = ToWords(bags);
        return $"{words} bao";
    }

    public static string ToVndWords(decimal amount)
    {
        var longAmt = (long)Math.Round(amount);
        var words = ToWords(longAmt);
        return $"{words} đồng";
    }

    private static string ReadThreeDigits(int number, bool fullPrefix)
    {
        var h = number / 100;
        var t = (number % 100) / 10;
        var o = number % 10;

        var parts = new List<string>();

        if (h > 0 || fullPrefix)
        {
            parts.Add(Digits[h] + " trăm");
        }

        if (t > 1)
        {
            parts.Add(Digits[t] + " mươi");
            if (o == 1) parts.Add("mốt");
            else if (o == 4) parts.Add("tư");
            else if (o == 5) parts.Add("lăm");
            else if (o > 0) parts.Add(Digits[o]);
        }
        else if (t == 1)
        {
            parts.Add("mười");
            if (o == 1) parts.Add("một");
            else if (o == 4) parts.Add("bốn");
            else if (o == 5) parts.Add("lăm");
            else if (o > 0) parts.Add(Digits[o]);
        }
        else // t == 0
        {
            if (h > 0 || fullPrefix)
            {
                if (o > 0)
                {
                    parts.Add("lẻ");
                    parts.Add(Digits[o]);
                }
            }
            else if (o > 0)
            {
                parts.Add(Digits[o]);
            }
        }

        return string.Join(" ", parts);
    }
}
