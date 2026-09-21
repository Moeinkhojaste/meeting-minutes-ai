using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MeetingMinutesAI.Domain.Users;
using MeetingMinutesAI.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MeetingMinutesAI.Infrastructure.Tests;

public sealed class JwtTokenServiceTests
{
    private readonly JwtOptions _options = new()
    {
        SecretKey = "a-very-long-and-secure-secret-key-that-is-at-least-32-chars-long",
        Issuer = "MeetingMinutesAI.Api",
        Audience = "MeetingMinutesAI.Client",
        ExpiryMinutes = 60,
    };

    [Fact]
    public void GenerateTokenProducesVerifiableSignedTokenWithCorrectClaims()
    {
        var now = DateTimeOffset.UtcNow;
        var service = new JwtTokenService(
            Options.Create(_options),
            new TestTimeProvider(now));

        var user = User.Create("alice@example.com", "hash", "Alice Smith", now);
        var result = service.GenerateToken(user);

        Assert.NotNull(result.Token);
        Assert.Equal(now.AddMinutes(60), result.ExpiresAt);

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        var principal = handler.ValidateToken(result.Token, validationParameters, out var validatedToken);
        Assert.NotNull(validatedToken);

        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        Assert.Equal(user.Id.ToString(), subClaim);

        var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        Assert.Equal("alice@example.com", emailClaim);

        var nameClaim = principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
        Assert.Equal("Alice Smith", nameClaim);
    }

    [Fact]
    public void TamperedTokenFailsCryptographicVerification()
    {
        var now = DateTimeOffset.UtcNow;
        var service = new JwtTokenService(
            Options.Create(_options),
            new TestTimeProvider(now));

        var user = User.Create("bob@example.com", "hash", "Bob Jones", now);
        var result = service.GenerateToken(user);

        // Tamper with the token payload
        var parts = result.Token.Split('.');
        var tamperedToken = $"{parts[0]}.eyJhYmMiOiJkZWYifQ.{parts[2]}";

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)),
            ValidateLifetime = false,
        };

        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(tamperedToken, validationParameters, out _));
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
