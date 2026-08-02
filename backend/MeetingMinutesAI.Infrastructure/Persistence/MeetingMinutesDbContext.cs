using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Domain.Meetings;
using Microsoft.EntityFrameworkCore;

namespace MeetingMinutesAI.Infrastructure.Persistence;

public sealed class MeetingMinutesDbContext : DbContext, IUnitOfWork
{
    public MeetingMinutesDbContext(DbContextOptions<MeetingMinutesDbContext> options)
        : base(options)
    {
    }

    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<AudioFile> AudioFiles => Set<AudioFile>();
    public DbSet<RawTranscript> RawTranscripts => Set<RawTranscript>();
    public DbSet<RawTranscriptSegment> RawTranscriptSegments => Set<RawTranscriptSegment>();
    public DbSet<CleanedTranscript> CleanedTranscripts => Set<CleanedTranscript>();
    public DbSet<CleanedTranscriptSegment> CleanedTranscriptSegments => Set<CleanedTranscriptSegment>();
    public DbSet<MeetingMinutes> MeetingMinutes => Set<MeetingMinutes>();
    public DbSet<ProcessingRun> ProcessingRuns => Set<ProcessingRun>();
    public DbSet<ProcessingStage> ProcessingStages => Set<ProcessingStage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeetingMinutesDbContext).Assembly);
    }
}
