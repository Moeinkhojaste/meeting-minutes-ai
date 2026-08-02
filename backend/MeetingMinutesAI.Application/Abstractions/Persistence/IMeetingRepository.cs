using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Application.Abstractions.Persistence;

public interface IMeetingRepository
{
    Task<Meeting?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Meeting meeting, CancellationToken cancellationToken = default);

    void Remove(Meeting meeting);
}
