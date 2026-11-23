using System.ComponentModel.DataAnnotations;
using BATT_PPMS.Helpers;
using Xunit;

namespace BATT_PPMS.UnitTests.Helpers;

public class ValidationAttributesTests
{
    [Theory]
    [InlineData("UserStatuses", "Active", true)]
    [InlineData("UserStatuses", "Locked", true)]
    [InlineData("UserStatuses", "Unknown", false)]
    [InlineData("PartnerStatuses", "Inactive", true)]
    [InlineData("PartnerStatuses", "Locked", false)]
    public void StatusSets_IsValid_ChecksExactMembership(string setName, string value, bool expected) =>
        Assert.Equal(expected, StatusSets.IsValid(StatusSets.Get(setName), value));

    [Fact]
    public void StatusSets_Get_UnknownSet_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => StatusSets.Get("MissingStatuses"));

    [Fact]
    public void StatusSets_IsValid_NullValue_IsAccepted() =>
        Assert.True(StatusSets.IsValid(StatusSets.UserStatuses, null));

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Locked", true)]
    [InlineData("Deleted", false)]
    [InlineData(null, true)]
    public void AllowedStatusAttribute_ValidatesStatus(string? value, bool expected)
    {
        var attribute = new AllowedStatusAttribute("UserStatuses");
        Assert.Equal(expected, attribute.IsValid(value));
    }

    [Fact]
    public void AllowedStatusAttribute_RejectsNonStringValue()
    {
        var attribute = new AllowedStatusAttribute("UserStatuses");
        Assert.False(attribute.IsValid(123));
    }

    [Fact]
    public void AllowedStatusAttribute_FormatsErrorMessage()
    {
        var attribute = new AllowedStatusAttribute("UserStatuses");
        var message = attribute.FormatErrorMessage("Trạng thái");
        Assert.Contains("Active", message);
        Assert.Contains("Inactive", message);
        Assert.Contains("Locked", message);
    }

    [Theory]
    [InlineData("1999-12-31", false)]
    [InlineData("2000-01-01", true)]
    [InlineData("2026-11-24", true)]
    [InlineData("2100-12-31", true)]
    [InlineData("2101-01-01", false)]
    public void SqlDateRangeAttribute_ValidatesDateTimeRange(string input, bool expected)
    {
        var value = DateTime.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, new SqlDateRangeAttribute().IsValid(value));
    }

    [Fact]
    public void SqlDateRangeAttribute_ValidatesDateOnlyRange()
    {
        var attribute = new SqlDateRangeAttribute();
        Assert.True(attribute.IsValid(new DateOnly(2000, 1, 1)));
        Assert.True(attribute.IsValid(new DateOnly(2100, 12, 31)));
        Assert.False(attribute.IsValid(new DateOnly(1999, 12, 31)));
    }

    [Fact]
    public void SqlDateRangeAttribute_NullIsAccepted() =>
        Assert.True(new SqlDateRangeAttribute().IsValid(null));

    [Fact]
    public void DataValidationException_StoresErrorsAndBuildsMessage()
    {
        var exception = new DataValidationException(new[] { "Tên bắt buộc", "Số điện thoại không hợp lệ" });
        Assert.Equal(2, exception.Errors.Count);
        Assert.Contains("Tên bắt buộc", exception.Message);
        Assert.Contains("Số điện thoại không hợp lệ", exception.Message);
    }

    [Fact]
    public void ModelValidation_Validate_ReturnsErrorsForInvalidObject()
    {
        var errors = ModelValidation.Validate(new SampleValidatedModel { Name = "" });
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ModelValidation_Validate_ReturnsEmptyForValidObject()
    {
        var errors = ModelValidation.Validate(new SampleValidatedModel { Name = "BATT" });
        Assert.Empty(errors);
    }

    private sealed class SampleValidatedModel
    {
        [Required]
        [StringLength(10)]
        public string? Name { get; set; }
    }
}