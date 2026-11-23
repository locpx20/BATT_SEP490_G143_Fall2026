using BATT_PPMS.Helpers;
using Xunit;

namespace BATT_PPMS.UnitTests.Helpers;

public class FinishedGoodsQuantityTests
{
    [Theory]
    [InlineData(800, "1 bao")]
    [InlineData(1600, "2 bao")]
    [InlineData(8000, "10 bao")]
    public void ForStaff_WholeBagQuantityAndValidLot_ShowsBags(decimal kg, string expected)
    {
        Assert.Equal(expected, FinishedGoodsQuantity.ForStaff(kg, "LOT-261124-C1-1-2"));
    }

    [Theory]
    [InlineData(750, "750,00 kg")]
    [InlineData(850, "850,00 kg")]
    [InlineData(1600, "1.600,00 kg")]
    public void ForStaff_NonWholeBagQuantity_ShowsKilograms(decimal kg, string expected)
    {
        Assert.Equal(expected, FinishedGoodsQuantity.ForStaff(kg, "LOT-261124-C1-1-2"));
    }

    [Fact]
    public void ForStaff_WholeBagQuantityButInvalidLot_ShowsKilograms()
    {
        Assert.Equal("800,00 kg", FinishedGoodsQuantity.ForStaff(800, "not-a-lot"));
    }

    [Fact]
    public void ForStaff_NullLot_ShowsKilograms()
    {
        Assert.Equal("800,00 kg", FinishedGoodsQuantity.ForStaff(800, null));
    }
}