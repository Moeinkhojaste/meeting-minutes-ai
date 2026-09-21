using MeetingMinutesAI.Application.Abstractions.Auth;
using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Application.Auth;
using MeetingMinutesAI.Domain.Common;
using MeetingMinutesAI.Domain.Users;

namespace MeetingMinutesAI.Application.Tests;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeJwtTokenService _jwtTokenService = new();
    private readonly TestTimeProvider _timeProvider = new(Now);

    private AuthService CreateService() =>
        new(_userRepository, _unitOfWork, _passwordHasher, _jwtTokenService, _timeProvider);

    [Fact]
    public async Task RegisterAsyncCreatesUserAndReturnsToken()
    {
        var service = CreateService();
        var command = new RegisterCommand("newuser@example.com", "SecurePassword123!", "New User");

        var result = await service.RegisterAsync(command);

        Assert.NotNull(result);
        Assert.Equal("token-for-newuser@example.com", result.Token);
        Assert.Equal("newuser@example.com", result.User.Email);
        Assert.Equal("New User", result.User.FullName);
        Assert.True(_unitOfWork.Saved);
    }

    [Fact]
    public async Task RegisterAsyncThrowsWhenEmailAlreadyExists()
    {
        var service = CreateService();
        var existing = User.Create("existing@example.com", "hash", "Existing", Now);
        await _userRepository.AddAsync(existing);

        var command = new RegisterCommand("existing@example.com", "SecurePassword123!", "New User");

        await Assert.ThrowsAsync<UserAlreadyExistsException>(() =>
            service.RegisterAsync(command));
    }

    [Fact]
    public async Task RegisterAsyncThrowsWhenPasswordTooShort()
    {
        var service = CreateService();
        var command = new RegisterCommand("user@example.com", "short", "User");

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.RegisterAsync(command));
    }

    [Fact]
    public async Task LoginAsyncReturnsTokenWhenCredentialsValid()
    {
        var service = CreateService();
        var user = User.Create("user@example.com", "hashed:Password123!", "User Name", Now);
        await _userRepository.AddAsync(user);

        var result = await service.LoginAsync(new LoginCommand("user@example.com", "Password123!"));

        Assert.NotNull(result);
        Assert.Equal("token-for-user@example.com", result.Token);
        Assert.Equal(user.Id, result.User.Id);
    }

    [Fact]
    public async Task LoginAsyncThrowsWhenPasswordIncorrect()
    {
        var service = CreateService();
        var user = User.Create("user@example.com", "hashed:Password123!", "User Name", Now);
        await _userRepository.AddAsync(user);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.LoginAsync(new LoginCommand("user@example.com", "WrongPassword!")));
    }

    [Fact]
    public async Task LoginAsyncThrowsWhenUserNotFound()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.LoginAsync(new LoginCommand("nonexistent@example.com", "Password123!")));
    }

    [Fact]
    public async Task GetCurrentUserAsyncReturnsUserView()
    {
        var service = CreateService();
        var user = User.Create("user@example.com", "hash", "User Name", Now);
        await _userRepository.AddAsync(user);

        var view = await service.GetCurrentUserAsync(user.Id);

        Assert.NotNull(view);
        Assert.Equal(user.Id, view.Id);
        Assert.Equal("user@example.com", view.Email);
        Assert.Equal("User Name", view.FullName);
    }

    [Fact]
    public async Task GetCurrentUserAsyncThrowsWhenUserNotFound()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            service.GetCurrentUserAsync(Guid.NewGuid()));
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly List<User> _users = [];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool Saved { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saved = true;
            return Task.FromResult(1);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"hashed:{password}";

        public bool VerifyPassword(string hashedPassword, string providedPassword) =>
            hashedPassword == $"hashed:{providedPassword}";
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public GeneratedToken GenerateToken(User user) =>
            new($"token-for-{user.Email}", DateTimeOffset.UtcNow.AddHours(1));
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
