using MeetingMinutesAI.Infrastructure.Auth;

namespace MeetingMinutesAI.Infrastructure.Tests;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void HashPasswordGeneratesVersionedHashWithSalt()
    {
        var password = "SecurePassword123!";
        var hash = _hasher.HashPassword(password);

        Assert.NotNull(hash);
        var parts = hash.Split('.');
        Assert.Equal(4, parts.Length);
        Assert.Equal("v1", parts[0]);
        Assert.Equal("100000", parts[1]);
        Assert.NotEmpty(parts[2]);
        Assert.NotEmpty(parts[3]);
    }

    [Fact]
    public void SamePasswordProducesDifferentHashesDueToRandomSalt()
    {
        var password = "SecurePassword123!";
        var hash1 = _hasher.HashPassword(password);
        var hash2 = _hasher.HashPassword(password);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyPasswordSucceedsWithCorrectPassword()
    {
        var password = "CorrectHorseBatteryStaple!";
        var hash = _hasher.HashPassword(password);

        var isValid = _hasher.VerifyPassword(hash, password);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPasswordFailsWithIncorrectPassword()
    {
        var password = "CorrectHorseBatteryStaple!";
        var hash = _hasher.HashPassword(password);

        var isValid = _hasher.VerifyPassword(hash, "WrongPassword");

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-hash")]
    [InlineData("v1.notanumber.salt.hash")]
    [InlineData("v2.100000.salt.hash")]
    public void VerifyPasswordFailsWithMalformedHash(string malformedHash)
    {
        var isValid = _hasher.VerifyPassword(malformedHash, "password");

        Assert.False(isValid);
    }
}
