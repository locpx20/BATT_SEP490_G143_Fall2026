using BATT_PPMS.Helpers;
using Xunit;

namespace BATT_PPMS.UnitTests.Helpers;

public class InputValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Clean_WhenNullOrWhitespace_ReturnsNull(string? value) => Assert.Null(InputValidator.Clean(value));

    [Theory]
    [InlineData("  hello  ", "hello")]
    [InlineData("abc", "abc")]
    public void Clean_TrimsValue(string value, string expected) => Assert.Equal(expected, InputValidator.Clean(value));

    [Fact]
    public void Text_RequiredAndEmpty_ReturnsRequiredError() =>
        Assert.Equal("Tên là bắt buộc.", InputValidator.Text(" ", "Tên", 10, true));

    [Fact]
    public void Text_OptionalAndEmpty_ReturnsNull() => Assert.Null(InputValidator.Text(null, "Tên", 10));

    [Fact]
    public void Text_AtMaxLength_IsValid() => Assert.Null(InputValidator.Text(new string('a', 10), "Tên", 10));

    [Fact]
    public void Text_OverMaxLength_ReturnsError() =>
        Assert.Equal("Tên không được vượt quá 10 ký tự.", InputValidator.Text(new string('a', 11), "Tên", 10));

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("name+tag@example.co.vn")]
    public void Email_ValidAddress_ReturnsNull(string email) => Assert.Null(InputValidator.Email(email));

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@")]
    public void Email_InvalidAddress_ReturnsError(string email) => Assert.NotNull(InputValidator.Email(email));

    [Fact]
    public void Email_Over100Characters_ReturnsLengthError() =>
        Assert.Contains("100", InputValidator.Email(new string('a', 90) + "@example.com")!);

    [Theory]
    [InlineData("0912345678")]
    [InlineData("+84 912 345 678")]
    [InlineData("(091) 234-5678")]
    public void Phone_ValidFormat_ReturnsNull(string phone) => Assert.Null(InputValidator.Phone(phone));

    [Theory]
    [InlineData("123")]
    [InlineData("abc123456")]
    public void Phone_InvalidFormat_ReturnsError(string phone) => Assert.NotNull(InputValidator.Phone(phone));

    [Fact]
    public void Phone_EmptyOptionalValue_ReturnsNull() => Assert.Null(InputValidator.Phone("  "));

    [Theory]
    [InlineData("user_01")]
    [InlineData("abc")]
    [InlineData("user.name-01")]
    public void Username_ValidValue_ReturnsNull(string username) => Assert.Null(InputValidator.Username(username));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("user name")]
    [InlineData("tênđăngnhập")]
    public void Username_InvalidValue_ReturnsError(string? username) => Assert.NotNull(InputValidator.Username(username));

    [Theory]
    [InlineData("abc123")]
    [InlineData("Abc!@#")]
    [InlineData("1234567890")]
    public void Password_ValidValue_ReturnsNull(string password) => Assert.Null(InputValidator.Password(password));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("a" + "12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901")]
    [InlineData(" mậtkhẩu123")]
    [InlineData("abc123 ")]
    public void Password_InvalidValue_ReturnsError(string? password) => Assert.NotNull(InputValidator.Password(password));

    [Fact]
    public void Status_KnownValue_ReturnsNull() =>
        Assert.Null(InputValidator.Status(" Active ", StatusSets.UserStatuses));

    [Fact]
    public void Status_UnknownValue_ReturnsError() =>
        Assert.NotNull(InputValidator.Status("Deleted", StatusSets.UserStatuses));

    [Fact]
    public void Status_NullValue_ReturnsNull() =>
        Assert.Null(InputValidator.Status(null, StatusSets.UserStatuses));

    [Fact]
    public void First_ReturnsFirstNonNullError() =>
        Assert.Equal("second", InputValidator.First(null, "second", "third"));

    [Fact]
    public void First_WhenAllNull_ReturnsNull() => Assert.Null(InputValidator.First(null, null));
}