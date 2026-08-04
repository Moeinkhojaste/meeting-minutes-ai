using System.Net;
using System.Text;
using MeetingMinutesAI.Application.Abstractions.Ai;
using MeetingMinutesAI.Domain.Meetings;
using MeetingMinutesAI.Infrastructure.Ai;
using Microsoft.Extensions.Options;

namespace MeetingMinutesAI.Infrastructure.Tests;

public sealed class AiServiceClientTests
{
    [Fact]
    public async Task TranscriptionUsesTheMultipartContractAndCorrelationHeader()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var client = CreateClient(async request =>
        {
            captured = request;
            body = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, TranscriptionJson());
        });

        var result = await client.TranscribeAsync(
            new MemoryStream("RIFF0000WAVE"u8.ToArray()),
            "safe.wav",
            "audio/wav",
            ProcessingMode.Quality,
            "corr-1");

        Assert.Equal("/v1/transcriptions", captured!.RequestUri!.AbsolutePath);
        Assert.Equal("corr-1", captured.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Contains("name=audio", body);
        Assert.Contains("filename=safe.wav", body);
        Assert.Contains("audio/wav", body);
        Assert.Contains("quality", body);
        Assert.Equal("seg-0001", result.RawTranscript.Segments.Single().Id);
    }

    [Fact]
    public async Task MinutesUsesStrictCamelCaseJsonAndRequestedMode()
    {
        string? body = null;
        var client = CreateClient(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, MinutesJson());
        });

        await client.GenerateMinutesAsync(RawTranscript(), ProcessingMode.Fast, "corr-1");

        Assert.Contains("\"rawTranscript\"", body);
        Assert.Contains("\"sourceRawSegmentIds\"", MinutesJson());
        Assert.Contains("\"mode\":\"fast\"", body);
        Assert.DoesNotContain("RawTranscript", body);
    }

    [Fact]
    public async Task UnknownOrOversizedResponsesAreRejected()
    {
        var unknown = CreateClient(_ => Task.FromResult(Json(
            HttpStatusCode.OK,
            TranscriptionJson().Replace(
                "\"correlationId\": \"corr-1\"",
                "\"correlationId\": \"corr-1\",\"unexpected\":true"))));
        var oversized = CreateClient(
            _ => Task.FromResult(Json(HttpStatusCode.OK, TranscriptionJson())),
            maxResponseBytes: 10);

        var malformedFailure = await Assert.ThrowsAsync<AiServiceException>(() =>
            unknown.TranscribeAsync(
                new MemoryStream([1]), "a.wav", "audio/wav",
                ProcessingMode.Fast, "corr-1"));
        var oversizedFailure = await Assert.ThrowsAsync<AiServiceException>(() =>
            oversized.TranscribeAsync(
                new MemoryStream([1]), "a.wav", "audio/wav",
                ProcessingMode.Fast, "corr-1"));

        Assert.Equal(AiFailureKind.InvalidResponse, malformedFailure.Kind);
        Assert.Equal(AiFailureKind.InvalidResponse, oversizedFailure.Kind);
    }

    [Fact]
    public async Task SafeRemoteErrorsAreMappedWithoutLeakingUnknownBodies()
    {
        var safe = CreateClient(_ => Task.FromResult(Json(
            HttpStatusCode.UnprocessableEntity,
            """{"code":"AUDIO_INVALID","message":"Audio is invalid.","correlationId":"corr-1","retryable":false}""")));
        var unsafeBody = CreateClient(_ => Task.FromResult(Json(
            HttpStatusCode.InternalServerError,
            """{"detail":"secret provider payload"}""")));

        var safeFailure = await Assert.ThrowsAsync<AiServiceException>(() =>
            safe.TranscribeAsync(new MemoryStream([1]), "a", "audio/wav",
                ProcessingMode.Fast, "corr-1"));
        var unsafeFailure = await Assert.ThrowsAsync<AiServiceException>(() =>
            unsafeBody.TranscribeAsync(new MemoryStream([1]), "a", "audio/wav",
                ProcessingMode.Fast, "corr-1"));

        Assert.Equal(AiFailureKind.InvalidRequest, safeFailure.Kind);
        Assert.Equal("AUDIO_INVALID", safeFailure.Code);
        Assert.Equal("Audio is invalid.", safeFailure.SafeMessage);
        Assert.Equal("AI_INVALID_RESPONSE", unsafeFailure.Code);
        Assert.DoesNotContain("secret", unsafeFailure.SafeMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NetworkFailureIsNotRetried()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            throw new HttpRequestException("offline");
        });

        var failure = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.TranscribeAsync(new MemoryStream([1]), "a", "audio/wav",
                ProcessingMode.Fast, "corr-1"));

        Assert.Equal(AiFailureKind.Unavailable, failure.Kind);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task TransportCancellationWithoutCallerCancellationIsATimeout()
    {
        var calls = 0;
        var client = CreateClient(_ =>
        {
            calls++;
            throw new OperationCanceledException("transport timeout");
        });

        var failure = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.TranscribeAsync(new MemoryStream([1]), "a", "audio/wav",
                ProcessingMode.Fast, "corr-1"));

        Assert.Equal(AiFailureKind.Timeout, failure.Kind);
        Assert.Equal("AI_SERVICE_TIMEOUT", failure.Code);
        Assert.Equal(1, calls);
    }

    private static AiServiceClient CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> send,
        long maxResponseBytes = 1_000_000)
    {
        var httpClient = new HttpClient(new DelegateHandler(send))
        {
            BaseAddress = new Uri("http://ai.test"),
            Timeout = TimeSpan.FromMinutes(1),
        };
        return new AiServiceClient(
            httpClient,
            Options.Create(new AiServiceOptions
            {
                MaxResponseBytes = maxResponseBytes,
            }));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static AiRawTranscript RawTranscript() => new(
        1,
        [new AiRawSegment("seg-0001", "Speaker", 0, 1000, "fa", "متن")]);

    private static string TranscriptionJson() => $$"""
        {
          "rawTranscript": {
            "schemaVersion": 1,
            "segments": [{
              "id": "seg-0001", "speaker": "Speaker", "startMilliseconds": 0,
              "endMilliseconds": 1000, "language": "fa", "text": "متن"
            }]
          },
          "metadata": {{MetadataJson("transcription")}},
          "correlationId": "corr-1"
        }
        """;

    private static string MinutesJson() => $$"""
        {
          "cleanedTranscript": {
            "schemaVersion": 1,
            "segments": [{"id":"clean-0001","text":"متن","sourceRawSegmentIds":["seg-0001"]}]
          },
          "minutes": {
            "schemaVersion": 1, "title": null, "date": null, "summary": "خلاصه",
            "participants": [], "topics": [],
            "decisions": [{"text":"تصمیم","evidenceSegmentIds":["seg-0001"]}],
            "actionItems": [], "openQuestions": [], "uncertainties": []
          },
          "metadata": {{MetadataJson("minutes")}},
          "correlationId": "corr-1"
        }
        """;

    private static string MetadataJson(string stage) => $$"""
        {
          "stage":"{{stage}}", "requestedMode":"fast", "primaryProvider":"gemini",
          "primaryModel":"gemini-3.5-flash-lite", "actualProvider":"gemini",
          "actualModel":"gemini-3.5-flash-lite", "fallbackUsed":false,
          "fallbackReason":null, "promptVersion":"v1", "schemaVersion":1,
          "startedAt":"2026-08-03T10:00:00Z", "completedAt":"2026-08-03T10:00:01Z",
          "durationMilliseconds":1000
        }
        """;

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request);
    }
}
