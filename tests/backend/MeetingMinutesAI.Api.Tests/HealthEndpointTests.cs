using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MeetingMinutesAI.Api.Tests;

public sealed class HealthEndpointTests :
    IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpointReturnsOk(string path)
    {
        using var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CorrelationIdIsReturned()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/health/live"
        );
        request.Headers.Add("X-Correlation-ID", "test-correlation-123");

        using var response = await _client.SendAsync(request);

        Assert.Equal(
            "test-correlation-123",
            response.Headers.GetValues("X-Correlation-ID").Single()
        );
    }
}
