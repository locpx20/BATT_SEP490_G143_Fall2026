using BATT_PPMS.Helpers;
using Xunit;

namespace BATT_PPMS.UnitTests.Helpers;

public class PaginationAndPricingTests
{
    [Fact]
    public void PageWindow_EmptyResult_HasOnePageAndZeroItems()
    {
        var window = new PageWindow(0, 1);
        Assert.Equal(1, window.TotalPages);
        Assert.Equal(1, window.CurrentPage);
        Assert.Equal(0, window.FirstItem);
        Assert.Equal(0, window.LastItem);
        Assert.Equal(0, window.Offset);
    }

    [Fact]
    public void PageWindow_CalculatesOffsetAndItemRange()
    {
        var window = new PageWindow(25, 2, 10);
        Assert.Equal(3, window.TotalPages);
        Assert.Equal(2, window.CurrentPage);
        Assert.Equal(10, window.Offset);
        Assert.Equal(11, window.FirstItem);
        Assert.Equal(20, window.LastItem);
    }

    [Fact]
    public void PageWindow_LastPageUsesRemainingItems()
    {
        var window = new PageWindow(25, 3, 10);
        Assert.Equal(21, window.FirstItem);
        Assert.Equal(25, window.LastItem);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(100, 3)]
    [InlineData(0, 1)]
    public void PageWindow_ClampsRequestedPage(int requested, int expected)
    {
        var window = new PageWindow(25, requested, 10);
        Assert.Equal(expected, window.CurrentPage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PageWindow_NonPositivePageSize_Throws(int size) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageWindow(10, 1, size));

    [Theory]
    [InlineData(null, "")]
    [InlineData(0, "0")]
    [InlineData(1200, "1200")]
    [InlineData(1200.50, "1200.5")]
    public void FormatInvariant_FormatsNullablePrice(decimal? price, string expected) =>
        Assert.Equal(expected, PurchasePricing.FormatInvariant(price));

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("", null, true)]
    [InlineData("1200", 1200, true)]
    [InlineData("1200.50", 1200.50, true)]
    [InlineData("1200", 1201, false)]
    [InlineData("not-a-number", 1200, false)]
    [InlineData("", 100, false)]
    public void MatchesCurrent_ComparesDisplayedAndCurrentPrice(string? shown, decimal? current, bool expected) =>
        Assert.Equal(expected, PurchasePricing.MatchesCurrent(shown, current));
}