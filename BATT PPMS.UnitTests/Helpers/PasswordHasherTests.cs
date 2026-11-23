using BATT_PPMS.Helpers;
using Xunit;

namespace BATT_PPMS.UnitTests.Helpers;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ReturnsExpectedMd5Hex()
    {
        Assert.Equal("e10adc3949ba59abbe56e057f20f883e", PasswordHasher.Hash("123456"));
    }

    [Fact]
    public void Hash_ReturnsLowercase32CharacterHex()
    {
        var hash = PasswordHasher.Hash("BATT@2026");
        Assert.Equal(32, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.Matches("^[0-9a-f]{32}$", hash);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = PasswordHasher.Hash("password123");
        Assert.True(PasswordHasher.Verify("password123", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.Hash("password123");
        Assert.False(PasswordHasher.Verify("wrong-password", hash));
    }

    [Theory]
    [InlineData(null, "d41d8cd98f00b204e9800998ecf8427e")]
    [InlineData("password", null)]
    [InlineData("password", "  ")]
    public void Verify_NullOrBlankArguments_ReturnsFalse(string? plainText, string? storedHash) =>
        Assert.False(PasswordHasher.Verify(plainText, storedHash));

    [Fact]
    public void Verify_StoredHashIsTrimmedAndCaseInsensitive()
    {
        var hash = PasswordHasher.Hash("secret");
        Assert.True(PasswordHasher.Verify("secret", "  " + hash.ToUpperInvariant() + "  "));
    }
}