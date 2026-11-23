using BATT_PPMS.Helpers;
using Xunit;

namespace BATT_PPMS.UnitTests.Helpers;

public class LotCodeTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData("Ca1", 1)]
    [InlineData(" ca 2 ", 2)]
    [InlineData("CA3", 3)]
    public void ShiftNumber_ValidShift_ReturnsNumber(string shift, int expected) =>
        Assert.Equal(expected, LotCode.ShiftNumber(shift));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ca0")]
    [InlineData("Ca4")]
    [InlineData("CaA")]
    [InlineData("Shift 1")]
    public void ShiftNumber_InvalidShift_ReturnsNull(string? shift) =>
        Assert.Null(LotCode.ShiftNumber(shift));

    [Fact]
    public void ShiftPrefix_FormatsDateAndShift()
    {
        Assert.Equal("LOT-261124-C2-", LotCode.ShiftPrefix(new DateOnly(2026, 11, 24), 2));
    }

    [Fact]
    public void Create_CombinesPrefixSequenceAndBagCount()
    {
        Assert.Equal("LOT-261124-C2-3-20", LotCode.Create("LOT-261124-C2-", 3, 20));
    }

    [Theory]
    [InlineData("LOT-261124-C2-3-20", 20)]
    [InlineData("LOT-260101-C1-1-1", 1)]
    public void BagCount_ValidLot_ReturnsBagCount(string lot, int expected) =>
        Assert.Equal(expected, LotCode.BagCount(lot));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("LOT-261124-C4-3-20")]
    [InlineData("LOT-261124-C2-0-20")]
    [InlineData("LOT-261124-C2-3-0")]
    [InlineData("LOT-991399-C2-3-20")]
    [InlineData("invalid")]
    public void BagCount_InvalidLot_ReturnsNull(string? lot) =>
        Assert.Null(LotCode.BagCount(lot));

    [Fact]
    public void ForListDisplay_LongValidCode_IsShortenedTo16Characters()
    {
        var code = "LOT-20261124-C2-3-20";
        Assert.Equal("LOT-20261124-C2-", LotCode.ForListDisplay(code));
    }

    [Theory]
    [InlineData("LOT-20261124-C2-")]
    [InlineData("OTHER-20261124-C2-3-20")]
    [InlineData("LOT-20269999-C2-3-20")]
    public void ForListDisplay_InvalidOrShortCode_IsReturnedUnchanged(string code) =>
        Assert.Equal(code, LotCode.ForListDisplay(code));
}