using System.Net;
using System.Net.Http.Json;
using MeetingMinutesAI.Api.Errors;
using MeetingMinutesAI.Api.Meetings;
using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Api.Tests;

public sealed class MeetingEndpointTests : IClassFixture<MeetingApiFactory>
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);

    private readonly MeetingApiFactory _factory;
    private readonly HttpClient _client;

    public MeetingEndpointTests(MeetingApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CrudFlowReturnsStatusesAndEnforcesEtags()
    {
        using var create = await _client.PostAsJsonAsync(
            "/api/meetings",
            new { title = "  Planning  " });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        var created = await create.Content.ReadFromJsonAsync<MeetingResponse>();
        Assert.NotNull(created);
        Assert.Equal("Planning", created.Title);
        Assert.Equal("created", created.Status);
        Assert.Null(created.ProcessingError);
        var originalEtag = Assert.Single(create.Headers.GetValues("ETag"));

        using var list = await _client.GetAsync("/api/meetings?page=1&pageSize=20");
        var page = await list.Content.ReadFromJsonAsync<MeetingPageResponse>();
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.NotNull(page);
        Assert.Contains(page.Items, item => item.Id == created.Id);

        using var get = await _client.GetAsync($"/api/meetings/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(originalEtag, Assert.Single(get.Headers.GetValues("ETag")));

        using var updateRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/meetings/{created.Id}")
        {
            Content = JsonContent.Create(new { title = "  Revised  " }),
        };
        updateRequest.Headers.TryAddWithoutValidation("If-Match", originalEtag);
        using var update = await _client.SendAsync(updateRequest);
        var updated = await update.Content.ReadFromJsonAsync<MeetingResponse>();
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("Revised", updated.Title);
        var updatedEtag = Assert.Single(update.Headers.GetValues("ETag"));
        Assert.NotEqual(originalEtag, updatedEtag);

        using var staleRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/meetings/{created.Id}")
        {
            Content = JsonContent.Create(new { title = "Stale" }),
        };
        staleRequest.Headers.TryAddWithoutValidation("If-Match", originalEtag);
        using var stale = await _client.SendAsync(staleRequest);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(
            "concurrency_conflict",
            (await stale.Content.ReadFromJsonAsync<ApiError>())!.Code);

        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/meetings/{created.Id}");
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", updatedEtag);
        using var delete = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var missing = await _client.GetAsync($"/api/meetings/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task InvalidPaginationAndPreconditionsUseSafeErrors()
    {
        using var create = await _client.PostAsJsonAsync(
            "/api/meetings",
            new { title = "Validation" });
        var meeting = await create.Content.ReadFromJsonAsync<MeetingResponse>();
        Assert.NotNull(meeting);

        using var pagination = await _client.GetAsync(
            "/api/meetings?page=0&pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, pagination.StatusCode);
        Assert.Equal(
            "invalid_request",
            (await pagination.Content.ReadFromJsonAsync<ApiError>())!.Code);

        using var missingHeader = await _client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}",
            new { title = "Changed" });
        Assert.Equal((HttpStatusCode)428, missingHeader.StatusCode);
        Assert.Equal(
            "precondition_required",
            (await missingHeader.Content.ReadFromJsonAsync<ApiError>())!.Code);

        using var invalidRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/meetings/{meeting.Id}")
        {
            Content = JsonContent.Create(new { title = "Changed" }),
        };
        invalidRequest.Headers.TryAddWithoutValidation("If-Match", "not-an-etag");
        using var invalidHeader = await _client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalidHeader.StatusCode);
        Assert.Equal(
            "invalid_version",
            (await invalidHeader.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    [Fact]
    public async Task FailedAndPartialStatusesIncludeSafeProcessingErrors()
    {
        var failed = CreateFailedMeeting();
        var partial = CreatePartialMeeting();
        var queued = CreateUploadedMeeting("Queued", "queued");
        queued.Queue(Now);
        await _factory.SeedAsync(failed);
        await _factory.SeedAsync(partial);
        await _factory.SeedAsync(queued);

        using var failedResponse = await _client.GetAsync($"/api/meetings/{failed.Id}");
        var failedBody = await failedResponse.Content.ReadFromJsonAsync<MeetingResponse>();
        Assert.NotNull(failedBody);
        Assert.Equal("failed", failedBody.Status);
        Assert.Equal("TRANSCRIPTION_FAILED", failedBody.ProcessingError!.Code);

        using var partialResponse = await _client.GetAsync($"/api/meetings/{partial.Id}");
        var partialBody = await partialResponse.Content.ReadFromJsonAsync<MeetingResponse>();
        Assert.NotNull(partialBody);
        Assert.Equal("partiallyCompleted", partialBody.Status);
        Assert.Equal("MINUTES_FAILED", partialBody.ProcessingError!.Code);

        using var queuedResponse = await _client.GetAsync($"/api/meetings/{queued.Id}");
        var queuedEtag = Assert.Single(queuedResponse.Headers.GetValues("ETag"));
        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/meetings/{queued.Id}");
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", queuedEtag);
        using var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
        Assert.Equal(
            "meeting_active",
            (await deleteResponse.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    private static Meeting CreateFailedMeeting()
    {
        var meeting = CreateUploadedMeeting("Failed", "failed");
        meeting.Queue(Now);
        var run = meeting.StartTranscription(
            ProcessingMode.Fast,
            "failed-correlation",
            Now);
        run.Fail("TRANSCRIPTION_FAILED", "Transcription failed.", true, Now, 1);
        meeting.Fail(run, "TRANSCRIPTION_FAILED", "Transcription failed.", Now);
        return meeting;
    }

    private static Meeting CreatePartialMeeting()
    {
        var meeting = CreateUploadedMeeting("Partial", "partial");
        meeting.Queue(Now);
        var run = meeting.StartTranscription(
            ProcessingMode.Quality,
            "partial-correlation",
            Now);
        meeting.SetRawTranscript(
            run,
            1,
            [RawTranscriptSegment.Create("segment-1", 0, null, 0, 100, "fa", "text")],
            Now);
        meeting.StartMinutesGeneration(run, Now);
        run.CompletePartially("MINUTES_FAILED", "Minutes failed.", true, Now, 2);
        meeting.CompletePartially(run, "MINUTES_FAILED", "Minutes failed.", Now);
        return meeting;
    }

    private static Meeting CreateUploadedMeeting(string title, string key)
    {
        var meeting = Meeting.Create(title, Now);
        meeting.AttachAudio(
            $"{key}.wav",
            $"audio/{key}",
            "audio/wav",
            10,
            100,
            new string(key[0], 64),
            Now);
        return meeting;
    }
}
