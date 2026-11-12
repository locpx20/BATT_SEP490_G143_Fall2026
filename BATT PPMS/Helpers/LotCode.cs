using System.Globalization;

namespace BATT_PPMS.Helpers;

public static class LotCode
{
    // Keep the 38-character LOT format used by main, with the production date.
    public static string Create(DateOnly productionDate) =>
        $"LOT-{productionDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}"[..38];

    public static string ForListDisplay(string code)
    {
        const int displayLength = 16; // LOT-yyyyMMdd- plus three suffix characters.
        if (code.Length <= displayLength || !code.StartsWith("LOT-", StringComparison.Ordinal) || code[12] != '-')
            return code;

        return DateOnly.TryParseExact(code.AsSpan(4, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? code[..displayLength]
            : code;
    }
}
