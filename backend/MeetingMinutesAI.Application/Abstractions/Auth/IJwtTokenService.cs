using MeetingMinutesAI.Domain.Users;

namespace MeetingMinutesAI.Application.Abstractions.Auth;

public sealed record GeneratedToken(string Token, DateTimeOffset ExpiresAt);

public interface IJwtTokenService
{
    GeneratedToken GenerateToken(User user);
}
