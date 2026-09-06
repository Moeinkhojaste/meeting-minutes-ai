using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace MeetingMinutesAI.Infrastructure.Persistence;

internal sealed class UserRepository : IUserRepository
{
    private readonly MeetingMinutesDbContext _context;

    public UserRepository(MeetingMinutesDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _context.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(user => user.Email == email, cancellationToken);

    public Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        _context.Users.AddAsync(user, cancellationToken).AsTask();
}
