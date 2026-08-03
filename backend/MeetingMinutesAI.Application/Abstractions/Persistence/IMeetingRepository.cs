using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Application.Abstractions.Persistence;

public interface IMeetingRepository
{
    Task<Meeting?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Meeting>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Meeting meeting, CancellationToken cancellationToken = default);

    void Remove(Meeting meeting);
}
