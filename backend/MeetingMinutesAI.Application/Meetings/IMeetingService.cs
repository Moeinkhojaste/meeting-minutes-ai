namespace MeetingMinutesAI.Application.Meetings;

public interface IMeetingService
{
    Task<MeetingView> CreateAsync(
        string? title,
        CancellationToken cancellationToken = default);

    Task<MeetingPage> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<MeetingView> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<MeetingView> UpdateAsync(
        Guid id,
        string? title,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default);
}
