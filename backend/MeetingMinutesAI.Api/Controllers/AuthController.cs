using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MeetingMinutesAI.Api.Errors;
using MeetingMinutesAI.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeetingMinutesAI.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthApiResponse>> Register(
        RegisterApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(
            new RegisterCommand(request.Email, request.Password, request.FullName),
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            AuthApiResponse.FromApplication(result));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthApiResponse>> Login(
        LoginApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);

        return Ok(AuthApiResponse.FromApplication(result));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserApiResponse>> GetCurrentUser(
        CancellationToken cancellationToken)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var userId))
        {
            return Unauthorized(new ApiError(
                "unauthorized",
                "User identifier is missing or invalid in token.",
                HttpContext.TraceIdentifier));
        }

        var user = await authService.GetCurrentUserAsync(userId, cancellationToken);
        return Ok(UserApiResponse.FromApplication(user));
    }
}

public sealed record RegisterApiRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password,
    [Required, MaxLength(200)] string FullName);

public sealed record LoginApiRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(128)] string Password);

public sealed record AuthApiResponse(
    string Token,
    string TokenType,
    UserApiResponse User,
    DateTimeOffset ExpiresAt)
{
    public static AuthApiResponse FromApplication(AuthResult result) =>
        new(
            result.Token,
            "Bearer",
            UserApiResponse.FromApplication(result.User),
            result.ExpiresAt);
}

public sealed record UserApiResponse(
    Guid Id,
    string Email,
    string FullName,
    DateTimeOffset CreatedAt)
{
    public static UserApiResponse FromApplication(UserView user) =>
        new(user.Id, user.Email, user.FullName, user.CreatedAt);
}
