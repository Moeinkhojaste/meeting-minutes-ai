namespace MeetingMinutesAI.Application.Meetings;

public interface IMeetingService
{
    Task<MeetingView> CreateAsync(
        string? title,
        string? userId = null,
        CancellationToken cancellationToken = default);

    Task<MeetingPage> ListAsync(
        int page,
        int pageSize,
        string? userId = null,
        CancellationToken cancellationToken = default);

    Task<MeetingView> GetByIdAsync(
        Guid id,
        string? userId = null,
        CancellationToken cancellationToken = default);

    Task<MeetingView> UpdateAsync(
        Guid id,
        string? title,
        byte[] expectedVersion,
        string? userId = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        byte[] expectedVersion,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
