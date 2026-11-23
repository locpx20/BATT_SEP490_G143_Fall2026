using BATT_PPMS.Helpers;
using Xunit;

namespace BATT_PPMS.UnitTests.Helpers;

public class SalesFlowPureTests
{
    [Theory]
    [InlineData("A", true)]
    [InlineData("B", true)]
    [InlineData("a", false)]
    [InlineData("C", false)]
    [InlineData(null, false)]
    public void IsGrade_OnlyAcceptsAOrB(string? grade, bool expected) =>
        Assert.Equal(expected, SalesFlow.IsGrade(grade));

    [Theory]
    [InlineData("A", "Loại A")]
    [InlineData("B", "Loại B")]
    [InlineData("C", "—")]
    [InlineData(null, "—")]
    public void GradeLabel_MapsGrade(string? grade, string expected) =>
        Assert.Equal(expected, SalesFlow.GradeLabel(grade));

    [Theory]
    [InlineData(800, "1")]
    [InlineData(1600, "2")]
    [InlineData(400, "0,5")]
    [InlineData(0, "0")]
    public void Bags_ConvertsKilogramsToBags(decimal kg, string expected) =>
        Assert.Equal(expected, SalesFlow.Bags(kg));

    [Fact]
    public void Allocate_ConsumesOldestLotsFirst()
    {
        var newer = new LotAvailability(2, "LOT-NEW", new DateOnly(2026, 11, 20), "A", 5);
        var older = new LotAvailability(1, "LOT-OLD", new DateOnly(2026, 11, 10), "A", 3);
        var allocations = SalesFlow.Allocate(new[] { newer, older }, 6)!;
        Assert.Equal(2, allocations.Count);
        Assert.Equal(1, allocations[0].BatchId);
        Assert.Equal(3, allocations[0].Bags);
        Assert.Equal(2, allocations[1].BatchId);
        Assert.Equal(3, allocations[1].Bags);
    }

    [Fact]
    public void Allocate_InsufficientStock_ReturnsNull()
    {
        var lots = new[] { new LotAvailability(1, "LOT-1", new DateOnly(2026, 11, 10), "A", 2) };
        Assert.Null(SalesFlow.Allocate(lots, 3));
    }

    [Fact]
    public void Allocate_ZeroBags_ReturnsEmptyAllocation()
    {
        var lots = new[] { new LotAvailability(1, "LOT-1", new DateOnly(2026, 11, 10), "A", 2) };
        Assert.Empty(SalesFlow.Allocate(lots, 0)!);
    }

    [Fact]
    public void LotAvailability_AvailableKg_Uses800KgPerBag()
    {
        var lot = new LotAvailability(1, "LOT-1", new DateOnly(2026, 11, 10), "A", 4);
        Assert.Equal(3200m, lot.AvailableKg);
    }

    [Fact]
    public void LotAllocation_Kg_Uses800KgPerBag()
    {
        var allocation = new LotAllocation(1, "LOT-1", new DateOnly(2026, 11, 10), "A", 3);
        Assert.Equal(2400m, allocation.Kg);
    }

    [Fact]
    public void NextCode_IncrementsHighestMatchingSequence()
    {
        var date = DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var code = SalesFlow.NextCode(
            new[] { $"EXP-{date}-001", $"EXP-{date}-004", $"OTHER-{date}-999" },
            "EXP");
        Assert.Equal($"EXP-{date}-005", code);
    }
}