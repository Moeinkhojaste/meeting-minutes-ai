using System.Net;
using System.Net.Http.Json;
using MeetingMinutesAI.Api.Errors;
using MeetingMinutesAI.Api.Meetings;
using MeetingMinutesAI.Application.Abstractions.Ai;

namespace MeetingMinutesAI.Api.Tests;

public sealed class MeetingMediaEndpointTests : IClassFixture<MeetingApiFactory>
{
    private readonly MeetingApiFactory _factory;
    private readonly HttpClient _client;

    public MeetingMediaEndpointTests(MeetingApiFactory factory)
    {
        _factory = factory;
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
        factory.AiClient.TranscriptionFailure = null;
        factory.AiClient.MinutesFailure = null;
    }

    [Fact]
    public async Task UploadAndProcessPersistSeparateOutputsAndAdvanceEtags()
    {
        var created = await CreateMeetingAsync();
        using var upload = await UploadAsync(
            created.Meeting.Id, created.ETag, WavBytes(), "../../private.wav");
        var uploaded = await upload.Content.ReadFromJsonAsync<MeetingResponse>();

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.NotNull(uploaded);
        Assert.Equal("uploaded", uploaded.Status);
        Assert.Equal("private.wav", uploaded.Audio!.OriginalFileName);
        Assert.Equal("audio/wav", uploaded.Audio.ContentType);
        Assert.Equal(WavBytes().Length, uploaded.Audio.ByteLength);
        var uploadBody = await upload.Content.ReadAsStringAsync();
        Assert.DoesNotContain("objects/", uploadBody);
        Assert.DoesNotContain("storageKey", uploadBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sha256", uploadBody, StringComparison.OrdinalIgnoreCase);
        var uploadedEtag = ETag(upload);
        Assert.NotEqual(created.ETag, uploadedEtag);

        using var processRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/api/meetings/{created.Meeting.Id}/process")
        {
            Content = JsonContent.Create(new { mode = "quality" }),
        };
        processRequest.Headers.TryAddWithoutValidation("If-Match", uploadedEtag);
        using var process = await _client.SendAsync(processRequest);
        var processed = await process.Content.ReadFromJsonAsync<MeetingResponse>();
        Assert.Equal(HttpStatusCode.OK, process.StatusCode);
        Assert.NotNull(processed);
        Assert.Equal("completed", processed.Status);
        var completedEtag = ETag(process);
        Assert.NotEqual(uploadedEtag, completedEtag);

        using var raw = await _client.GetAsync(
            $"/api/meetings/{created.Meeting.Id}/transcripts/raw");
        using var cleaned = await _client.GetAsync(
            $"/api/meetings/{created.Meeting.Id}/transcripts/cleaned");
        using var minutes = await _client.GetAsync(
            $"/api/meetings/{created.Meeting.Id}/minutes/generated");
        var rawBody = await raw.Content.ReadFromJsonAsync<RawTranscriptResponse>();
        var cleanedBody = await cleaned.Content.ReadFromJsonAsync<CleanedTranscriptResponse>();
        var minutesBody = await minutes.Content.ReadFromJsonAsync<GeneratedMinutesResponse>();

        Assert.Equal(HttpStatusCode.OK, raw.StatusCode);
        Assert.Equal("seg-0001", rawBody!.Segments.Single().Id);
        Assert.Equal(["seg-0001"],
            cleanedBody!.Segments.Single().SourceRawSegmentIds);
        Assert.Equal(["seg-0001"],
            minutesBody!.Decisions.Single().EvidenceSegmentIds);
        Assert.Equal(completedEtag, ETag(raw));
        Assert.Equal(completedEtag, ETag(cleaned));
        Assert.Equal(completedEtag, ETag(minutes));

        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete, $"/api/meetings/{created.Meeting.Id}");
        deleteRequest.Headers.TryAddWithoutValidation("If-Match", completedEtag);
        using var delete = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync(
                $"/api/meetings/{created.Meeting.Id}/transcripts/raw")).StatusCode);
    }

    [Fact]
    public async Task UploadValidatesPreconditionsSignatureSizeAndCurrentVersion()
    {
        var missingHeader = await CreateMeetingAsync();
        using var missing = await UploadAsync(
            missingHeader.Meeting.Id, null, WavBytes(), "meeting.wav");
        Assert.Equal((HttpStatusCode)428, missing.StatusCode);

        var spoofedMeeting = await CreateMeetingAsync();
        using var spoofed = await UploadAsync(
            spoofedMeeting.Meeting.Id,
            spoofedMeeting.ETag,
            "not really audio"u8.ToArray(),
            "meeting.mp3");
        Assert.Equal(HttpStatusCode.BadRequest, spoofed.StatusCode);
        Assert.Equal("unsupported_audio",
            (await spoofed.Content.ReadFromJsonAsync<ApiError>())!.Code);

        var oversizedMeeting = await CreateMeetingAsync();
        var oversizedBytes = new byte[65];
        WavBytes().CopyTo(oversizedBytes, 0);
        using var oversized = await UploadAsync(
            oversizedMeeting.Meeting.Id,
            oversizedMeeting.ETag,
            oversizedBytes,
            "meeting.wav");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        Assert.Equal("audio_too_large",
            (await oversized.Content.ReadFromJsonAsync<ApiError>())!.Code);

        var staleMeeting = await CreateMeetingAsync();
        using var stale = await UploadAsync(
            staleMeeting.Meeting.Id,
            "\"AAAAAAAAAAA=\"",
            WavBytes(),
            "meeting.wav");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency_conflict",
            (await stale.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    [Fact]
    public async Task StageTwoFailureReturnsPartialAndOnlyRawOutput()
    {
        var created = await CreateMeetingAsync();
        using var upload = await UploadAsync(
            created.Meeting.Id, created.ETag, WavBytes(), "meeting.wav");
        _factory.AiClient.MinutesFailure = new AiServiceException(
            AiFailureKind.Unavailable,
            "GEMINI_UNAVAILABLE",
            "Minutes generation is unavailable.",
            true);

        try
        {
            using var processRequest = new HttpRequestMessage(
                HttpMethod.Post, $"/api/meetings/{created.Meeting.Id}/process")
            {
                Content = JsonContent.Create(new { mode = "fast" }),
            };
            processRequest.Headers.TryAddWithoutValidation("If-Match", ETag(upload));
            using var process = await _client.SendAsync(processRequest);
            var body = await process.Content.ReadFromJsonAsync<MeetingResponse>();

            Assert.Equal(HttpStatusCode.OK, process.StatusCode);
            Assert.Equal("partiallyCompleted", body!.Status);
            Assert.Equal("GEMINI_UNAVAILABLE", body.ProcessingError!.Code);
            Assert.Equal(HttpStatusCode.OK,
                (await _client.GetAsync(
                    $"/api/meetings/{created.Meeting.Id}/transcripts/raw")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await _client.GetAsync(
                    $"/api/meetings/{created.Meeting.Id}/transcripts/cleaned")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await _client.GetAsync(
                    $"/api/meetings/{created.Meeting.Id}/minutes/generated")).StatusCode);
        }
        finally
        {
            _factory.AiClient.MinutesFailure = null;
        }
    }

    [Fact]
    public async Task StageOneProviderRejectionReturns422AfterPersistingFailedStatus()
    {
        var created = await CreateMeetingAsync();
        using var upload = await UploadAsync(
            created.Meeting.Id, created.ETag, WavBytes(), "meeting.wav");
        _factory.AiClient.TranscriptionFailure = new AiServiceException(
            AiFailureKind.InvalidRequest,
            "AUDIO_REJECTED",
            "Audio was rejected.",
            false);

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"/api/meetings/{created.Meeting.Id}/process")
            {
                Content = JsonContent.Create(new { mode = "fast" }),
            };
            request.Headers.TryAddWithoutValidation("If-Match", ETag(upload));
            using var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("AUDIO_REJECTED",
                (await response.Content.ReadFromJsonAsync<ApiError>())!.Code);

            using var get = await _client.GetAsync($"/api/meetings/{created.Meeting.Id}");
            var meeting = await get.Content.ReadFromJsonAsync<MeetingResponse>();
            Assert.Equal("failed", meeting!.Status);
            Assert.Equal("AUDIO_REJECTED", meeting.ProcessingError!.Code);
        }
        finally
        {
            _factory.AiClient.TranscriptionFailure = null;
        }
    }

    [Fact]
    public async Task CompletedMeetingCanBeExplicitlyProcessedAgain()
    {
        var created = await CreateMeetingAsync();
        using var upload = await UploadAsync(
            created.Meeting.Id, created.ETag, WavBytes(), "meeting.wav");
        using var firstRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/api/meetings/{created.Meeting.Id}/process");
        firstRequest.Headers.TryAddWithoutValidation("If-Match", ETag(upload));
        using var first = await _client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var secondRequest = ProcessRequest(
            created.Meeting.Id, ETag(first), "quality");
        using var second = await _client.SendAsync(secondRequest);
        var body = await second.Content.ReadFromJsonAsync<MeetingResponse>();

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("completed", body!.Status);
        Assert.NotEqual(ETag(first), ETag(second));
    }

    private async Task<(MeetingResponse Meeting, string ETag)> CreateMeetingAsync()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/meetings", new { title = "Media test" });
        return (
            (await response.Content.ReadFromJsonAsync<MeetingResponse>())!,
            ETag(response));
    }

    private async Task<HttpResponseMessage> UploadAsync(
        Guid id,
        string? etag,
        byte[] bytes,
        string fileName)
    {
        using var multipart = new MultipartFormDataContent();
        var audio = new ByteArrayContent(bytes);
        audio.Headers.ContentType = new("application/octet-stream");
        multipart.Add(audio, "audio", fileName);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/meetings/{id}/audio")
        {
            Content = multipart,
        };
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }
        return await _client.SendAsync(request);
    }

    private static HttpRequestMessage ProcessRequest(Guid id, string etag, string mode)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/meetings/{id}/process")
        {
            Content = JsonContent.Create(new { mode }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return request;
    }

    private static string ETag(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("ETag"));

    private static byte[] WavBytes() => "RIFF0000WAVEfmt "u8.ToArray();
}
