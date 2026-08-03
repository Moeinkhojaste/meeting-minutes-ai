using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Domain.Meetings;
using MeetingMinutesAI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MeetingMinutesAI.Api.Tests;

public sealed class MeetingApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public MeetingApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MeetingMinutesDbContext>>();
            services.RemoveAll<MeetingMinutesDbContext>();
            services.RemoveAll<IUnitOfWork>();

            services.AddScoped<MeetingMinutesDbContext>(_ =>
            {
                var options = new DbContextOptionsBuilder<MeetingMinutesDbContext>()
                    .UseSqlite(_connection)
                    .Options;
                return new TestMeetingMinutesDbContext(options);
            });
            services.AddScoped<IUnitOfWork, TestUnitOfWork>();
        });
    }

    public void EnsureDatabaseCreated()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MeetingMinutesDbContext>();
        context.Database.EnsureCreated();
    }

    public async Task SeedAsync(Meeting meeting)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MeetingMinutesDbContext>();
        context.Meetings.Add(meeting);
        await context.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private sealed class TestUnitOfWork(MeetingMinutesDbContext context) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default) =>
            context.SaveChangesAsync(cancellationToken);
    }

    private sealed class TestMeetingMinutesDbContext(
        DbContextOptions<MeetingMinutesDbContext> options)
        : MeetingMinutesDbContext(options)
    {
        private static long _nextVersion;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var property in modelBuilder.Model
                .GetEntityTypes()
                .SelectMany(entity => entity.GetProperties()))
            {
                property.SetColumnType(null);
            }

            ConfigureTestRowVersion<Meeting>(modelBuilder);
            ConfigureTestRowVersion<RawTranscript>(modelBuilder);
            ConfigureTestRowVersion<CleanedTranscript>(modelBuilder);
            ConfigureTestRowVersion<MeetingMinutes>(modelBuilder);
            modelBuilder.Entity<Meeting>()
                .Property(meeting => meeting.UpdatedAt)
                .HasConversion(
                    value => value.UtcDateTime.Ticks,
                    value => new DateTimeOffset(value, TimeSpan.Zero));
        }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
            {
                var property = entry.Metadata.FindProperty("RowVersion");
                if (property is not null)
                {
                    entry.Property("RowVersion").CurrentValue =
                        BitConverter.GetBytes(Interlocked.Increment(ref _nextVersion));
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        private static void ConfigureTestRowVersion<TEntity>(ModelBuilder modelBuilder)
            where TEntity : class =>
            modelBuilder.Entity<TEntity>()
                .Property<byte[]>("RowVersion")
                .ValueGeneratedNever()
                .IsConcurrencyToken();
    }
}
