namespace MeetingMinutesAI.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class PersistenceConcurrencyException : Exception
{
    public PersistenceConcurrencyException(Exception innerException)
        : base("The persisted value changed during the operation.", innerException)
    {
    }
}
