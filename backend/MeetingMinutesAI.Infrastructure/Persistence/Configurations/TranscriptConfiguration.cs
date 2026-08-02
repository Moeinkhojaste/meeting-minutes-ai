using MeetingMinutesAI.Domain.Meetings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeetingMinutesAI.Infrastructure.Persistence.Configurations;

internal sealed class RawTranscriptConfiguration : IEntityTypeConfiguration<RawTranscript>
{
    public void Configure(EntityTypeBuilder<RawTranscript> builder)
    {
        builder.ToTable("RawTranscripts");
        builder.HasKey(transcript => transcript.Id);
        builder.Property(transcript => transcript.Id).ValueGeneratedNever();
        builder.Property(transcript => transcript.RowVersion).IsRowVersion();
        builder.HasIndex(transcript => transcript.MeetingId).IsUnique();
        builder.HasOne<ProcessingRun>()
            .WithMany()
            .HasForeignKey(transcript => transcript.ProcessingRunId)
            .OnDelete(DeleteBehavior.NoAction);

        var segments = builder.HasMany(transcript => transcript.Segments)
            .WithOne()
            .HasForeignKey(segment => segment.RawTranscriptId)
            .OnDelete(DeleteBehavior.Cascade);
        segments.Metadata.PrincipalToDependent!.SetField("_segments");
    }
}

internal sealed class RawTranscriptSegmentConfiguration :
    IEntityTypeConfiguration<RawTranscriptSegment>
{
    public void Configure(EntityTypeBuilder<RawTranscriptSegment> builder)
    {
        builder.ToTable("RawTranscriptSegments");
        builder.HasKey(segment => segment.Id);
        builder.Property(segment => segment.Id).ValueGeneratedNever();
        builder.Property(segment => segment.ExternalId).HasMaxLength(64).IsRequired();
        builder.Property(segment => segment.Speaker).HasMaxLength(200);
        builder.Property(segment => segment.Language).HasMaxLength(32).IsRequired();
        builder.Property(segment => segment.Text).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(segment => new { segment.RawTranscriptId, segment.ExternalId }).IsUnique();
        builder.HasIndex(segment => new { segment.RawTranscriptId, segment.Position }).IsUnique();
    }
}

internal sealed class CleanedTranscriptConfiguration : IEntityTypeConfiguration<CleanedTranscript>
{
    public void Configure(EntityTypeBuilder<CleanedTranscript> builder)
    {
        builder.ToTable("CleanedTranscripts");
        builder.HasKey(transcript => transcript.Id);
        builder.Property(transcript => transcript.Id).ValueGeneratedNever();
        builder.Property(transcript => transcript.RowVersion).IsRowVersion();
        builder.HasIndex(transcript => transcript.MeetingId).IsUnique();
        builder.HasOne<ProcessingRun>()
            .WithMany()
            .HasForeignKey(transcript => transcript.ProcessingRunId)
            .OnDelete(DeleteBehavior.NoAction);

        var segments = builder.HasMany(transcript => transcript.Segments)
            .WithOne()
            .HasForeignKey(segment => segment.CleanedTranscriptId)
            .OnDelete(DeleteBehavior.Cascade);
        segments.Metadata.PrincipalToDependent!.SetField("_segments");
    }
}

internal sealed class CleanedTranscriptSegmentConfiguration :
    IEntityTypeConfiguration<CleanedTranscriptSegment>
{
    public void Configure(EntityTypeBuilder<CleanedTranscriptSegment> builder)
    {
        builder.ToTable("CleanedTranscriptSegments");
        builder.HasKey(segment => segment.Id);
        builder.Property(segment => segment.Id).ValueGeneratedNever();
        builder.Property(segment => segment.ExternalId).HasMaxLength(64).IsRequired();
        builder.Property(segment => segment.Text).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(segment => new { segment.CleanedTranscriptId, segment.ExternalId }).IsUnique();
        builder.HasIndex(segment => new { segment.CleanedTranscriptId, segment.Position }).IsUnique();

        var sources = builder.HasMany(segment => segment.Sources)
            .WithOne()
            .HasForeignKey(source => source.CleanedTranscriptSegmentId)
            .OnDelete(DeleteBehavior.Cascade);
        sources.Metadata.PrincipalToDependent!.SetField("_sources");
    }
}

internal sealed class CleanedSegmentSourceConfiguration :
    IEntityTypeConfiguration<CleanedSegmentSource>
{
    public void Configure(EntityTypeBuilder<CleanedSegmentSource> builder)
    {
        builder.ToTable("CleanedSegmentSources");
        builder.HasKey(source => new
        {
            source.CleanedTranscriptSegmentId,
            source.RawTranscriptSegmentId,
        });
        builder.HasOne(source => source.RawSegment)
            .WithMany()
            .HasForeignKey(source => source.RawTranscriptSegmentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
