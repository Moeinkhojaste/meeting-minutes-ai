using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MeetingMinutesAI.Api.Controllers;
using MeetingMinutesAI.Api.Meetings;

namespace MeetingMinutesAI.Api.Tests;

public sealed class AuthEndpointTests : IClassFixture<MeetingApiFactory>
{
    private readonly MeetingApiFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointTests(MeetingApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterReturnsCreatedWithJwtToken()
    {
        var email = $"register_{Guid.NewGuid():N}@example.com";
        var request = new RegisterApiRequest(email, "SecurePassword123!", "John Doe");

        using var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthApiResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(email.ToLowerInvariant(), result.User.Email);
        Assert.Equal("John Doe", result.User.FullName);
        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task RegisterWithDuplicateEmailReturnsConflict()
    {
        var email = $"duplicate_{Guid.NewGuid():N}@example.com";
        var request = new RegisterApiRequest(email, "SecurePassword123!", "User One");

        using var firstResponse = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var secondResponse = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task LoginReturnsOkWithJwtTokenWhenCredentialsAreValid()
    {
        var email = $"login_{Guid.NewGuid():N}@example.com";
        var password = "CorrectPassword123!";
        var registerRequest = new RegisterApiRequest(email, password, "Alice");
        using var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        var loginRequest = new LoginApiRequest(email, password);
        using var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthApiResponse>();
        Assert.NotNull(auth);
        Assert.NotEmpty(auth.Token);
        Assert.Equal(email.ToLowerInvariant(), auth.User.Email);
    }

    [Fact]
    public async Task LoginReturnsUnauthorizedWhenPasswordIsIncorrect()
    {
        var email = $"wrongpass_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterApiRequest(email, "CorrectPassword123!", "Alice");
        using var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        var loginRequest = new LoginApiRequest(email, "WrongPassword999!");
        using var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUserReturnsUserWhenBearerTokenIsValid()
    {
        var email = $"me_{Guid.NewGuid():N}@example.com";
        var password = "Password123!";
        var registerRequest = new RegisterApiRequest(email, password, "Me Tester");
        using var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthApiResponse>();
        Assert.NotNull(auth);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        using var meResponse = await _client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var user = await meResponse.Content.ReadFromJsonAsync<UserApiResponse>();
        Assert.NotNull(user);
        Assert.Equal(auth.User.Id, user.Id);
        Assert.Equal(email.ToLowerInvariant(), user.Email);
        Assert.Equal("Me Tester", user.FullName);
    }

    [Fact]
    public async Task GetCurrentUserReturnsUnauthorizedWhenNoTokenProvided()
    {
        using var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUserReturnsUnauthorizedWhenTokenIsInvalidOrTampered()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedMeetingCreationLinksMeetingToUser()
    {
        var email = $"owner_{Guid.NewGuid():N}@example.com";
        var registerRequest = new RegisterApiRequest(email, "Password123!", "Owner");
        using var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthApiResponse>();
        Assert.NotNull(auth);

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/meetings")
        {
            Content = JsonContent.Create(new { title = "My Authenticated Meeting" }),
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        using var createResponse = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<MeetingResponse>();
        Assert.NotNull(created);

        // Fetching meeting as owner succeeds
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/meetings/{created.Id}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        using var getResponse = await _client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // Another registered user cannot access this meeting (Forbidden 403)
        var otherEmail = $"other_{Guid.NewGuid():N}@example.com";
        var otherRegister = new RegisterApiRequest(otherEmail, "Password123!", "Other User");
        using var otherRegResponse = await _client.PostAsJsonAsync("/api/auth/register", otherRegister);
        var otherAuth = await otherRegResponse.Content.ReadFromJsonAsync<AuthApiResponse>();
        Assert.NotNull(otherAuth);

        using var forbiddenRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/meetings/{created.Id}");
        forbiddenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherAuth.Token);
        using var forbiddenResponse = await _client.SendAsync(forbiddenRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }
}
