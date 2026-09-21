using MeetingMinutesAI.Domain.Common;
using MeetingMinutesAI.Domain.Users;

namespace MeetingMinutesAI.Domain.Tests;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateInitializesUserWithNormalizedEmailAndTimestamps()
    {
        var user = User.Create("  Test.User@Example.COM  ", "hash123", "  Test User  ", Now);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("test.user@example.com", user.Email);
        Assert.Equal("hash123", user.PasswordHash);
        Assert.Equal("Test User", user.FullName);
        Assert.Equal(Now, user.CreatedAt);
        Assert.Equal(Now, user.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-email")]
    [InlineData("@missingusername.com")]
    [InlineData("username@missingdomain")]
    [InlineData("username@domain.")]
    public void CreateRejectsInvalidEmail(string invalidEmail)
    {
        Assert.Throws<DomainRuleException>(() =>
            User.Create(invalidEmail, "hash123", "Test User", Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRejectsEmptyFullName(string emptyFullName)
    {
        Assert.Throws<DomainRuleException>(() =>
            User.Create("user@example.com", "hash123", emptyFullName, Now));
    }

    [Fact]
    public void UpdateProfileModifiesNameAndTimestamp()
    {
        var user = User.Create("user@example.com", "hash123", "Old Name", Now);
        var later = Now.AddHours(2);

        user.UpdateProfile("New Name", later);

        Assert.Equal("New Name", user.FullName);
        Assert.Equal(later, user.UpdatedAt);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Fact]
    public void ChangePasswordUpdatesHashAndTimestamp()
    {
        var user = User.Create("user@example.com", "hash123", "User", Now);
        var later = Now.AddHours(3);

        user.ChangePassword("newHash456", later);

        Assert.Equal("newHash456", user.PasswordHash);
        Assert.Equal(later, user.UpdatedAt);
    }
}
