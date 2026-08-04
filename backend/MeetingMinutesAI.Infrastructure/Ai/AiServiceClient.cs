using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MeetingMinutesAI.Application.Abstractions.Ai;
using MeetingMinutesAI.Domain.Meetings;
using Microsoft.Extensions.Options;

namespace MeetingMinutesAI.Infrastructure.Ai;

internal sealed class AiServiceClient(
    HttpClient httpClient,
    IOptions<AiServiceOptions> options) : IAiServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) },
    };

    private readonly long _maximumResponseBytes = options.Value.MaxResponseBytes;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);

    public async Task<AiTranscriptionResult> TranscribeAsync(
        Stream audio,
        string fileName,
        string contentType,
        ProcessingMode mode,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        using var multipart = new MultipartFormDataContent();
        using var audioContent = new StreamContent(audio);
        audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        multipart.Add(audioContent, "audio", fileName);
        multipart.Add(new StringContent(Mode(mode)), "mode");
        using var request = CreateRequest(HttpMethod.Post, "/v1/transcriptions", correlationId);
        request.Content = multipart;
        var response = await SendAsync<TranscriptionResponse>(
            request, correlationId, cancellationToken);
        return new AiTranscriptionResult(
            response.RawTranscript,
            response.Metadata,
            response.CorrelationId);
    }

    public async Task<AiMinutesResult> GenerateMinutesAsync(
        AiRawTranscript rawTranscript,
        ProcessingMode mode,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "/v1/minutes", correlationId);
        request.Content = JsonContent.Create(
            new MinutesRequest(rawTranscript, Mode(mode)),
            options: JsonOptions);
        var response = await SendAsync<MinutesResponse>(
            request, correlationId, cancellationToken);
        return new AiMinutesResult(
            response.CleanedTranscript,
            response.Minutes,
            response.Metadata,
            response.CorrelationId);
    }

    private async Task<T> SendAsync<T>(
        HttpRequestMessage request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            var bytes = await ReadBoundedAsync(response.Content, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw CreateRemoteFailure(response.StatusCode, bytes, correlationId);
            }

            try
            {
                return JsonSerializer.Deserialize<T>(bytes, JsonOptions)
                    ?? throw new JsonException("Response body is empty.");
            }
            catch (JsonException exception)
            {
                throw InvalidResponse(exception);
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiServiceException(
                AiFailureKind.Timeout,
                "AI_SERVICE_TIMEOUT",
                "The AI service timed out.",
                true,
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AiServiceException(
                AiFailureKind.Unavailable,
                "AI_SERVICE_UNAVAILABLE",
                "The AI service is unavailable.",
                true,
                exception);
        }
    }

    private async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > _maximumResponseBytes)
        {
            throw InvalidResponse();
        }

        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (destination.Length + read > _maximumResponseBytes)
            {
                throw InvalidResponse();
            }
            destination.Write(buffer, 0, read);
        }
        return destination.ToArray();
    }

    private static AiServiceException CreateRemoteFailure(
        HttpStatusCode statusCode,
        byte[] body,
        string correlationId)
    {
        try
        {
            var error = JsonSerializer.Deserialize<SafeErrorResponse>(body, JsonOptions);
            if (error is null
                || string.IsNullOrWhiteSpace(error.Code)
                || string.IsNullOrWhiteSpace(error.Message)
                || !string.Equals(
                    error.CorrelationId,
                    correlationId,
                    StringComparison.Ordinal))
            {
                throw new JsonException("Error response is incomplete.");
            }

            var kind = statusCode switch
            {
                HttpStatusCode.UnprocessableEntity => AiFailureKind.InvalidRequest,
                HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout =>
                    AiFailureKind.Timeout,
                _ => AiFailureKind.Unavailable,
            };
            return new AiServiceException(
                kind,
                error.Code,
                error.Message,
                error.Retryable);
        }
        catch (JsonException exception)
        {
            return InvalidResponse(exception);
        }
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        string correlationId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
        return request;
    }

    private static string Mode(ProcessingMode mode) => mode switch
    {
        ProcessingMode.Fast => "fast",
        ProcessingMode.Quality => "quality",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    private static AiServiceException InvalidResponse(Exception? exception = null) =>
        new(
            AiFailureKind.InvalidResponse,
            "AI_INVALID_RESPONSE",
            "The AI service returned an invalid response.",
            false,
            exception);

    private sealed record MinutesRequest(AiRawTranscript RawTranscript, string Mode);
    private sealed record TranscriptionResponse(
        AiRawTranscript RawTranscript,
        AiStageMetadata Metadata,
        string CorrelationId);
    private sealed record MinutesResponse(
        AiCleanedTranscript CleanedTranscript,
        AiGeneratedMinutes Minutes,
        AiStageMetadata Metadata,
        string CorrelationId);
    private sealed record SafeErrorResponse(
        string Code,
        string Message,
        string CorrelationId,
        bool Retryable);
}
