using BATT_PPMS.Helpers;
using Xunit;

namespace BATT_PPMS.UnitTests.Helpers;

public class PhoneHelperTests
{
    [Theory]
    [InlineData("0912345678", "0912345678")]
    [InlineData("+84912345678", "0912345678")]
    [InlineData("0084912345678", "0912345678")]
    [InlineData(" +84 (912) 345-678 ", "0912345678")]
    [InlineData("09.123.456.78", "0912345678")]
    public void Normalize_ConvertsToLocalDigits(string input, string expected) =>
        Assert.Equal(expected, PhoneHelper.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_EmptyInput_ReturnsEmpty(string? input) =>
        Assert.Equal(string.Empty, PhoneHelper.Normalize(input));

    [Theory]
    [InlineData("0912345678")]
    [InlineData("09123456789")]
    [InlineData("+84912345678")]
    [InlineData("0084912345678")]
    public void Validate_ValidPhone_ReturnsNull(string phone) =>
        Assert.Null(PhoneHelper.Validate(phone));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456789")]
    [InlineData("091234567")]
    [InlineData("091234567890")]
    [InlineData("abcdefghij")]
    public void Validate_InvalidPhone_ReturnsError(string? phone) =>
        Assert.NotNull(PhoneHelper.Validate(phone));
}