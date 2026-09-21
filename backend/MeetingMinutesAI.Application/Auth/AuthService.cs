using MeetingMinutesAI.Application.Abstractions.Auth;
using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Domain.Common;
using MeetingMinutesAI.Domain.Users;

namespace MeetingMinutesAI.Application.Auth;

public sealed class AuthService(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    TimeProvider timeProvider) : IAuthService
{
    private const int MinimumPasswordLength = 8;
    private const int MaximumPasswordLength = 128;

    public async Task<AuthResult> RegisterAsync(
        RegisterCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidatePassword(command.Password);

        var normalizedEmail = command.Email?.Trim().ToLowerInvariant()
            ?? throw new DomainRuleException("Email is required.");

        if (await userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new UserAlreadyExistsException(normalizedEmail);
        }

        var passwordHash = passwordHasher.HashPassword(command.Password);
        var now = timeProvider.GetUtcNow();
        var user = User.Create(
            normalizedEmail,
            passwordHash,
            command.FullName,
            now);

        await userRepository.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var token = jwtTokenService.GenerateToken(user);
        return new AuthResult(
            token.Token,
            MapUser(user),
            token.ExpiresAt);
    }

    public async Task<AuthResult> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            throw new InvalidCredentialsException();
        }

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        if (!passwordHasher.VerifyPassword(user.PasswordHash, command.Password))
        {
            throw new InvalidCredentialsException();
        }

        var token = jwtTokenService.GenerateToken(user);
        return new AuthResult(
            token.Token,
            MapUser(user),
            token.ExpiresAt);
    }

    public async Task<UserView> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UserNotFoundException(userId);

        return MapUser(user);
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new DomainRuleException("Password is required.");
        }

        if (password.Length < MinimumPasswordLength)
        {
            throw new DomainRuleException(
                $"Password must be at least {MinimumPasswordLength} characters.");
        }

        if (password.Length > MaximumPasswordLength)
        {
            throw new DomainRuleException(
                $"Password exceeds the maximum length of {MaximumPasswordLength} characters.");
        }
    }

    private static UserView MapUser(User user) =>
        new(user.Id, user.Email, user.FullName, user.CreatedAt);
}
