namespace MeetingMinutesAI.Application.Auth;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FullName);

public sealed record LoginCommand(
    string Email,
    string Password);

public sealed record AuthResult(
    string Token,
    UserView User,
    DateTimeOffset ExpiresAt);

public sealed record UserView(
    Guid Id,
    string Email,
    string FullName,
    DateTimeOffset CreatedAt);
