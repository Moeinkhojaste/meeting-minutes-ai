using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Application.Abstractions.Storage;
using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Application.Meetings;

public sealed class MeetingService(
    IMeetingRepository repository,
    IUnitOfWork unitOfWork,
    IAudioStorage audioStorage,
    TimeProvider timeProvider) : IMeetingService
{
    public async Task<MeetingView> CreateAsync(
        string? title,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var meeting = Meeting.Create(title, timeProvider.GetUtcNow(), userId);
        await repository.AddAsync(meeting, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return MeetingViewMapper.Map(meeting);
    }

    public async Task<MeetingPage> ListAsync(
        int page,
        int pageSize,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await repository.CountAsync(cancellationToken);
        var skip = (long)(page - 1) * pageSize;
        IReadOnlyList<Meeting> meetings = skip >= totalCount
            ? []
            : await repository.ListAsync((int)skip, pageSize, cancellationToken);

        if (userId is not null)
        {
            meetings = meetings.Where(item => item.UserId is null || string.Equals(item.UserId, userId, StringComparison.Ordinal)).ToList();
        }

        return new MeetingPage(
            meetings.Select(MeetingViewMapper.Map).ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<MeetingView> GetByIdAsync(
        Guid id,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var meeting = await GetRequiredAsync(id, userId, cancellationToken);
        return MeetingViewMapper.Map(meeting);
    }

    public async Task<MeetingView> UpdateAsync(
        Guid id,
        string? title,
        byte[] expectedVersion,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var meeting = await GetRequiredAsync(id, userId, cancellationToken);
        EnsureVersion(meeting, expectedVersion);
        meeting.Rename(title, timeProvider.GetUtcNow());
        await SaveChangesAsync(cancellationToken);
        return MeetingViewMapper.Map(meeting);
    }

    public async Task DeleteAsync(
        Guid id,
        byte[] expectedVersion,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var meeting = await GetRequiredAsync(id, userId, cancellationToken);
        EnsureVersion(meeting, expectedVersion);
        if (meeting.Status is MeetingProcessingStatus.Queued
            or MeetingProcessingStatus.Transcribing
            or MeetingProcessingStatus.GeneratingMinutes)
        {
            throw new MeetingActiveProcessingException();
        }

        await using var stagedDeletion = meeting.AudioFile is null
            ? null
            : await audioStorage.StageDeleteAsync(
                meeting.AudioFile.StorageKey,
                cancellationToken);
        repository.Remove(meeting);
        await SaveChangesAsync(cancellationToken);
        if (stagedDeletion is not null)
        {
            await stagedDeletion.CommitAsync(CancellationToken.None);
        }
    }

    private async Task<Meeting> GetRequiredAsync(
        Guid id,
        string? userId,
        CancellationToken cancellationToken)
    {
        var meeting = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new MeetingNotFoundException();

        if (meeting.UserId is not null && userId is not null && !string.Equals(meeting.UserId, userId, StringComparison.Ordinal))
        {
            throw new MeetingForbiddenException();
        }

        return meeting;
    }

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
}
