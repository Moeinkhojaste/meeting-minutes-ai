using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Domain.Meetings;
using Microsoft.EntityFrameworkCore;

namespace MeetingMinutesAI.Infrastructure.Persistence;

internal sealed class MeetingRepository : IMeetingRepository
{
    private readonly MeetingMinutesDbContext _context;

    public MeetingRepository(MeetingMinutesDbContext context)
    {
        _context = context;
    }

    public Task<Meeting?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        _context.Meetings
            .Include(meeting => meeting.AudioFile)
            .Include(meeting => meeting.RawTranscript)!
                .ThenInclude(transcript => transcript!.Segments)
            .Include(meeting => meeting.CleanedTranscript)!
                .ThenInclude(transcript => transcript!.Segments)
                    .ThenInclude(segment => segment.Sources)
            .Include(meeting => meeting.ProcessingRuns)
                .ThenInclude(run => run.Stages)
            .Include(meeting => meeting.Minutes)
                .ThenInclude(minutes => minutes.Participants)
                    .ThenInclude(item => item.Evidence)
            .Include(meeting => meeting.Minutes)
                .ThenInclude(minutes => minutes.Topics)
                    .ThenInclude(item => item.Evidence)
            .Include(meeting => meeting.Minutes)
                .ThenInclude(minutes => minutes.Decisions)
                    .ThenInclude(item => item.Evidence)
            .Include(meeting => meeting.Minutes)
                .ThenInclude(minutes => minutes.ActionItems)
                    .ThenInclude(item => item.Evidence)
            .Include(meeting => meeting.Minutes)
                .ThenInclude(minutes => minutes.OpenQuestions)
                    .ThenInclude(item => item.Evidence)
            .Include(meeting => meeting.Minutes)
                .ThenInclude(minutes => minutes.Uncertainties)
                    .ThenInclude(item => item.Evidence)
            .AsSplitQuery()
            .SingleOrDefaultAsync(meeting => meeting.Id == id, cancellationToken);

    public Task AddAsync(Meeting meeting, CancellationToken cancellationToken = default) =>
        _context.Meetings.AddAsync(meeting, cancellationToken).AsTask();

    public void Remove(Meeting meeting) => _context.Meetings.Remove(meeting);
}
