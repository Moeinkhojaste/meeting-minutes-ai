using System.ComponentModel.DataAnnotations;
using MeetingMinutesAI.Api.Errors;
using MeetingMinutesAI.Application.Meetings;
using MeetingMinutesAI.Domain.Meetings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace MeetingMinutesAI.Api.Meetings;

[ApiController]
[Route("api/meetings")]
public sealed class MeetingsController(
    IMeetingService meetingService,
    IMeetingMediaService mediaService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<MeetingResponse>> Create(
        CreateMeetingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var meeting = MeetingResponse.FromApplication(
            await meetingService.CreateAsync(request.Title, userId, cancellationToken));
        SetEntityTag(meeting);
        return CreatedAtAction(nameof(GetById), new { id = meeting.Id }, meeting);
    }

    [HttpGet]
    public async Task<ActionResult<MeetingPageResponse>> List(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await meetingService.ListAsync(page, pageSize, userId, cancellationToken);
        return Ok(MeetingPageResponse.FromApplication(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MeetingResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var meeting = MeetingResponse.FromApplication(
                await meetingService.GetByIdAsync(id, userId, cancellationToken));
            SetEntityTag(meeting);
            return Ok(meeting);
        }
        catch (MeetingForbiddenException)
        {
            return Error(StatusCodes.Status403Forbidden, "forbidden", "Access to the requested meeting is forbidden.");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MeetingResponse>> Update(
        Guid id,
        UpdateMeetingRequest request,
        CancellationToken cancellationToken)
    {
        var precondition = ParseEntityTag(out var expectedVersion);
        if (precondition is not null)
        {
            return precondition;
        }

        var userId = GetUserId();
        try
        {
            var meeting = MeetingResponse.FromApplication(
                await meetingService.UpdateAsync(
                    id,
                    request.Title,
                    expectedVersion,
                    userId,
                    cancellationToken));
            SetEntityTag(meeting);
            return Ok(meeting);
        }
        catch (MeetingForbiddenException)
        {
            return Error(StatusCodes.Status403Forbidden, "forbidden", "Access to the requested meeting is forbidden.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var precondition = ParseEntityTag(out var expectedVersion);
        if (precondition is not null)
        {
            return precondition;
        }

        var userId = GetUserId();
        try
        {
            await meetingService.DeleteAsync(id, expectedVersion, userId, cancellationToken);
            return NoContent();
        }
        catch (MeetingForbiddenException)
        {
            return Error(StatusCodes.Status403Forbidden, "forbidden", "Access to the requested meeting is forbidden.");
        }
    }

    [HttpPost("{id:guid}/audio")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(525_336_576)]
    public async Task<ActionResult<MeetingResponse>> UploadAudio(
        Guid id,
        CancellationToken cancellationToken)
    {
        var precondition = ParseEntityTag(out var expectedVersion);
        if (precondition is not null)
        {
            return precondition;
        }

        if (!Request.HasFormContentType)
        {
            return Error(400, "invalid_audio", "A multipart audio file is required.");
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        if (form.Files.Count != 1
            || !string.Equals(form.Files[0].Name, "audio", StringComparison.Ordinal)
            || form.Files[0].Length == 0)
        {
            return Error(400, "invalid_audio", "Exactly one non-empty audio file is required.");
        }

        var file = form.Files[0];
        await using var stream = file.OpenReadStream();
        var meeting = MeetingResponse.FromApplication(
            await mediaService.UploadAudioAsync(
                id,
                file.FileName,
                stream,
                expectedVersion,
                cancellationToken));
        SetEntityTag(meeting.Version);
        return Ok(meeting);
    }

    [HttpPost("{id:guid}/process")]
    public async Task<ActionResult<MeetingResponse>> Process(
        Guid id,
        [FromBody] ProcessMeetingRequest? request,
        CancellationToken cancellationToken)
    {
        var precondition = ParseEntityTag(out var expectedVersion);
        if (precondition is not null)
        {
            return precondition;
        }

        if (!TryParseMode(request?.Mode, out var mode))
        {
            return Error(400, "invalid_mode", "Mode must be fast or quality.");
        }

        var meeting = MeetingResponse.FromApplication(
            await mediaService.ProcessAsync(
                id,
                mode,
                expectedVersion,
                HttpContext.TraceIdentifier,
                cancellationToken));
        SetEntityTag(meeting.Version);
        return Ok(meeting);
    }

    [HttpGet("{id:guid}/transcripts/raw")]
    public async Task<ActionResult<RawTranscriptResponse>> GetRawTranscript(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = RawTranscriptResponse.FromApplication(
            await mediaService.GetRawTranscriptAsync(id, cancellationToken));
        SetEntityTag(response.Version);
        return Ok(response);
    }

    [HttpGet("{id:guid}/transcripts/cleaned")]
    public async Task<ActionResult<CleanedTranscriptResponse>> GetCleanedTranscript(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = CleanedTranscriptResponse.FromApplication(
            await mediaService.GetCleanedTranscriptAsync(id, cancellationToken));
        SetEntityTag(response.Version);
        return Ok(response);
    }

    [HttpGet("{id:guid}/minutes/generated")]
    public async Task<ActionResult<GeneratedMinutesResponse>> GetGeneratedMinutes(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = GeneratedMinutesResponse.FromApplication(
            await mediaService.GetGeneratedMinutesAsync(id, cancellationToken));
        SetEntityTag(response.Version);
        return Ok(response);
    }

    private string? GetUserId() =>
        Request.Headers.TryGetValue("X-User-Id", out var values) ? values.FirstOrDefault() : null;

    private ObjectResult? ParseEntityTag(out byte[] version)
    {
        Request.Headers.TryGetValue(HeaderNames.IfMatch, out var values);
        var result = EntityTagVersion.Parse(values, out version);
        return result switch
        {
            EntityTagParseResult.Success => null,
            EntityTagParseResult.Missing => Error(
                StatusCodes.Status428PreconditionRequired,
                "precondition_required",
                "A current If-Match value is required."),
            _ => Error(
                StatusCodes.Status400BadRequest,
                "invalid_version",
                "The If-Match value is invalid."),
        };
    }

    private ObjectResult Error(int statusCode, string code, string message) =>
        StatusCode(
            statusCode,
            new ApiError(code, message, HttpContext.TraceIdentifier));

    private static bool TryParseMode(string? value, out ProcessingMode mode)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "fast", StringComparison.Ordinal))
        {
            mode = ProcessingMode.Fast;
            return true;
        }

        if (string.Equals(value, "quality", StringComparison.Ordinal))
        {
            mode = ProcessingMode.Quality;
            return true;
        }

        mode = default;
        return false;
    }

    private void SetEntityTag(MeetingResponse meeting) => SetEntityTag(meeting.Version);

    private void SetEntityTag(string version) =>
        Response.Headers.ETag = EntityTagVersion.Format(version);
}
