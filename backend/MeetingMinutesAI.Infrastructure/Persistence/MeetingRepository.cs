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

    public async Task<IReadOnlyList<Meeting>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await _context.Meetings
            .AsNoTracking()
            .Include(meeting => meeting.AudioFile)
            .Include(meeting => meeting.ProcessingRuns)
                .ThenInclude(run => run.Stages)
            .OrderByDescending(meeting => meeting.UpdatedAt)
            .ThenByDescending(meeting => meeting.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        _context.Meetings.CountAsync(cancellationToken);

    public Task AddAsync(Meeting meeting, CancellationToken cancellationToken = default) =>
        _context.Meetings.AddAsync(meeting, cancellationToken).AsTask();

    public void Remove(Meeting meeting)
    {
        // SQL Server uses NO ACTION on cross-graph processing/evidence links to
        // avoid multiple cascade paths. Mark those tracked dependents first so
        // EF can issue deletes in a valid order without changing the schema.
        if (meeting.CleanedTranscript is not null)
        {
            _context.RemoveRange(meeting.CleanedTranscript.Segments
                .SelectMany(segment => segment.Sources));
        }

        foreach (var minutes in meeting.Minutes)
        {
            _context.RemoveRange(minutes.Participants.SelectMany(item => item.Evidence));
            _context.RemoveRange(minutes.Topics.SelectMany(item => item.Evidence));
            _context.RemoveRange(minutes.Decisions.SelectMany(item => item.Evidence));
            _context.RemoveRange(minutes.ActionItems.SelectMany(item => item.Evidence));
            _context.RemoveRange(minutes.OpenQuestions.SelectMany(item => item.Evidence));
            _context.RemoveRange(minutes.Uncertainties.SelectMany(item => item.Evidence));
        }

        if (meeting.CleanedTranscript is not null)
        {
            _context.CleanedTranscripts.Remove(meeting.CleanedTranscript);
        }
        if (meeting.RawTranscript is not null)
        {
            _context.RawTranscripts.Remove(meeting.RawTranscript);
        }
        _context.MeetingMinutes.RemoveRange(meeting.Minutes);
        _context.ProcessingRuns.RemoveRange(meeting.ProcessingRuns);
        _context.Meetings.Remove(meeting);
    }
}
