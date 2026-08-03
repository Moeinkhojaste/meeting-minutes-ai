using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Application.Meetings;

public sealed class MeetingService(
    IMeetingRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IMeetingService
{
    public async Task<MeetingView> CreateAsync(
        string? title,
        CancellationToken cancellationToken = default)
    {
        var meeting = Meeting.Create(title, timeProvider.GetUtcNow());
        await repository.AddAsync(meeting, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return Map(meeting);
    }

    public async Task<MeetingPage> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await repository.CountAsync(cancellationToken);
        var skip = (long)(page - 1) * pageSize;
        IReadOnlyList<Meeting> meetings = skip >= totalCount
            ? []
            : await repository.ListAsync((int)skip, pageSize, cancellationToken);
        return new MeetingPage(
            meetings.Select(Map).ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<MeetingView> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var meeting = await GetRequiredAsync(id, cancellationToken);
        return Map(meeting);
    }

    public async Task<MeetingView> UpdateAsync(
        Guid id,
        string? title,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var meeting = await GetRequiredAsync(id, cancellationToken);
        EnsureVersion(meeting, expectedVersion);
        meeting.Rename(title, timeProvider.GetUtcNow());
        await SaveChangesAsync(cancellationToken);
        return Map(meeting);
    }

    public async Task DeleteAsync(
        Guid id,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var meeting = await GetRequiredAsync(id, cancellationToken);
        EnsureVersion(meeting, expectedVersion);
        if (meeting.Status is MeetingProcessingStatus.Queued
            or MeetingProcessingStatus.Transcribing
            or MeetingProcessingStatus.GeneratingMinutes)
        {
            throw new MeetingActiveProcessingException();
        }

        repository.Remove(meeting);
        await SaveChangesAsync(cancellationToken);
    }

    private async Task<Meeting> GetRequiredAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new MeetingNotFoundException();

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (PersistenceConcurrencyException)
        {
            throw new MeetingConcurrencyException();
        }
    }

    private static void EnsureVersion(Meeting meeting, byte[] expectedVersion)
    {
        if (!meeting.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            throw new MeetingConcurrencyException();
        }
    }

    private static MeetingView Map(Meeting meeting) =>
        new(
            meeting.Id,
            meeting.Title,
            meeting.Status,
            meeting.ProcessingErrorCode,
            meeting.ProcessingErrorMessage,
            meeting.CreatedAt,
            meeting.UpdatedAt,
            meeting.RowVersion.ToArray());
}
