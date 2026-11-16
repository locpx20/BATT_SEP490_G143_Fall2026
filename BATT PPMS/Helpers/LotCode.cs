using System.Globalization;

namespace BATT_PPMS.Helpers;

public static class LotCode
{
    public static int? ShiftNumber(string? shift)
    {
        var name = shift?.Trim();
        if (name?.StartsWith("Ca", StringComparison.OrdinalIgnoreCase) == true)
            name = name[2..].Trim();
        return int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is >= 1 and <= 3
            ? number : null;
    }

    public static string ShiftPrefix(DateOnly productionDate, int shiftNumber) =>
        FormattableString.Invariant($"LOT-{productionDate:yyMMdd}-C{shiftNumber}-");

    public static string Create(string prefix, int sequence, int bags) =>
        FormattableString.Invariant($"{prefix}{sequence}-{bags}");

    public static int? BagCount(string? lot)
    {
        var parts = lot?.Split('-');
        return parts is { Length: 5 } && parts[0] == "LOT"
            && DateOnly.TryParseExact(parts[1], "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            && parts[2] is "C1" or "C2" or "C3"
            && int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) && sequence > 0
            && int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var bags) && bags > 0
            ? bags : null;
    }

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
