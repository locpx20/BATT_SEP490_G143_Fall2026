using System.Globalization;

namespace BATT_PPMS.Helpers;

public static class FinishedGoodsQuantity
{
    public const int KilogramsPerBag = 800;
    public const int MaxBags = (int)(9999999999.99m / KilogramsPerBag);

    public static string ForStaff(decimal kilograms, string? lot)
    {
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        return LotCode.BagCount(lot).HasValue && kilograms % KilogramsPerBag == 0
            ? $"{(kilograms / KilogramsPerBag).ToString("N0", vi)} bao"
            : $"{kilograms.ToString("N2", vi)} kg";
    }
}
