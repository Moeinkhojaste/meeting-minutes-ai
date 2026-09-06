using System.Text.RegularExpressions;
using MeetingMinutesAI.Domain.Common;

namespace MeetingMinutesAI.Domain.Users;

public sealed partial class User
{
    private User()
    {
    }

    private User(
        Guid id,
        string email,
        string passwordHash,
        string fullName,
        DateTimeOffset createdAt)
    {
        Id = id;
        Email = ValidateEmail(email);
        PasswordHash = Guard.Required(passwordHash, "Password hash", 500);
        FullName = Guard.Required(fullName, "Full name", 200);
        CreatedAt = Guard.Utc(createdAt);
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static User Create(
        string email,
        string passwordHash,
        string fullName,
        DateTimeOffset now) =>
        new(Guid.NewGuid(), email, passwordHash, fullName, now);

    public void UpdateProfile(string fullName, DateTimeOffset now)
    {
        FullName = Guard.Required(fullName, "Full name", 200);
        UpdatedAt = Guard.Utc(now);
    }

    public void ChangePassword(string newPasswordHash, DateTimeOffset now)
    {
        PasswordHash = Guard.Required(newPasswordHash, "Password hash", 500);
        UpdatedAt = Guard.Utc(now);
    }

    private static string ValidateEmail(string value)
    {
        var trimmed = Guard.Required(value, "Email", 256).ToLowerInvariant();
        if (!EmailRegex().IsMatch(trimmed))
        {
            throw new DomainRuleException("Email is not valid.");
        }
        return trimmed;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();
}
