using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Application.Abstractions.Ai;
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
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), "MeetingMinutesAI.Api.Tests", Guid.NewGuid().ToString("N"));

    public TestAiServiceClient AiClient { get; } = new();

    public MeetingApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("AudioStorage:RootPath", _storageRoot);
        builder.UseSetting("AudioStorage:MaxBytes", "64");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MeetingMinutesDbContext>>();
            services.RemoveAll<MeetingMinutesDbContext>();
            services.RemoveAll<IUnitOfWork>();
            services.RemoveAll<IAiServiceClient>();

            services.AddScoped<MeetingMinutesDbContext>(_ =>
            {
                var options = new DbContextOptionsBuilder<MeetingMinutesDbContext>()
                    .UseSqlite(_connection)
                    .Options;
                return new TestMeetingMinutesDbContext(options);
            });
            services.AddScoped<IUnitOfWork, TestUnitOfWork>();
            services.AddSingleton<IAiServiceClient>(AiClient);
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
            if (Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, recursive: true);
            }
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

    public sealed class TestAiServiceClient : IAiServiceClient
    {
        public AiServiceException? TranscriptionFailure { get; set; }
        public AiServiceException? MinutesFailure { get; set; }

        public Task<AiTranscriptionResult> TranscribeAsync(
            Stream audio,
            string fileName,
            string contentType,
            ProcessingMode mode,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            if (TranscriptionFailure is not null)
            {
                return Task.FromException<AiTranscriptionResult>(TranscriptionFailure);
            }

            return Task.FromResult(new AiTranscriptionResult(
                new AiRawTranscript(1,
                    [new AiRawSegment("seg-0001", "Speaker", 0, 1000, "fa", "متن خام")]),
                Metadata("transcription", mode),
                correlationId));
        }

        public Task<AiMinutesResult> GenerateMinutesAsync(
            AiRawTranscript rawTranscript,
            ProcessingMode mode,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            if (MinutesFailure is not null)
            {
                return Task.FromException<AiMinutesResult>(MinutesFailure);
            }

            return Task.FromResult(new AiMinutesResult(
                new AiCleanedTranscript(1,
                    [new AiCleanedSegment("clean-0001", "متن پاک", ["seg-0001"])]),
                new AiGeneratedMinutes(
                    1, "جلسه", null, "خلاصه", [], [],
                    [new AiDecision("تصمیم", ["seg-0001"])], [], [], []),
                Metadata("minutes", mode),
                correlationId));
        }

        private static AiStageMetadata Metadata(string stage, ProcessingMode mode)
        {
            var now = DateTimeOffset.UtcNow;
            var model = mode == ProcessingMode.Fast
                ? "gemini-3.5-flash-lite"
                : "gemini-3.6-flash";
            return new AiStageMetadata(
                stage, mode, "gemini", model, "gemini", model, false, null,
                "test-v1", 1, now, now, 0);
        }
    }
}
