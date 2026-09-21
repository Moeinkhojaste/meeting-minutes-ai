namespace MeetingMinutesAI.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(
        RegisterCommand command,
        CancellationToken cancellationToken = default);

    Task<AuthResult> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default);

    Task<UserView> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
