using System.ComponentModel.DataAnnotations;
using MeetingMinutesAI.Api.Errors;
using MeetingMinutesAI.Application.Meetings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace MeetingMinutesAI.Api.Meetings;

[ApiController]
[Route("api/meetings")]
public sealed class MeetingsController(IMeetingService meetingService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<MeetingResponse>> Create(
        CreateMeetingRequest request,
        CancellationToken cancellationToken)
    {
        var meeting = MeetingResponse.FromApplication(
            await meetingService.CreateAsync(request.Title, cancellationToken));
        SetEntityTag(meeting);
        return CreatedAtAction(nameof(GetById), new { id = meeting.Id }, meeting);
    }

    [HttpGet]
    public async Task<ActionResult<MeetingPageResponse>> List(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await meetingService.ListAsync(page, pageSize, cancellationToken);
        return Ok(MeetingPageResponse.FromApplication(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MeetingResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var meeting = MeetingResponse.FromApplication(
            await meetingService.GetByIdAsync(id, cancellationToken));
        SetEntityTag(meeting);
        return Ok(meeting);
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

        var meeting = MeetingResponse.FromApplication(
            await meetingService.UpdateAsync(
                id,
                request.Title,
                expectedVersion,
                cancellationToken));
        SetEntityTag(meeting);
        return Ok(meeting);
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

        await meetingService.DeleteAsync(id, expectedVersion, cancellationToken);
        return NoContent();
    }

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

    private void SetEntityTag(MeetingResponse meeting) =>
        Response.Headers.ETag = EntityTagVersion.Format(meeting.Version);
}
