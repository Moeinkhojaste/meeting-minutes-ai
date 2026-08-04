using MeetingMinutesAI.Domain.Meetings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeetingMinutesAI.Infrastructure.Persistence.Configurations;

internal sealed class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("Meetings");
        builder.HasKey(meeting => meeting.Id);
        builder.Property(meeting => meeting.Id).ValueGeneratedNever();
        builder.Property(meeting => meeting.Title).HasMaxLength(300);
        builder.Property(meeting => meeting.UserId).HasMaxLength(128);
        builder.Property(meeting => meeting.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(meeting => meeting.ProcessingErrorCode).HasMaxLength(100);
        builder.Property(meeting => meeting.ProcessingErrorMessage).HasMaxLength(1000);
        builder.Property(meeting => meeting.RowVersion).IsRowVersion();

        builder.HasOne(meeting => meeting.AudioFile)
            .WithOne()
            .HasForeignKey<AudioFile>(audio => audio.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(meeting => meeting.RawTranscript)
            .WithOne()
            .HasForeignKey<RawTranscript>(transcript => transcript.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(meeting => meeting.CleanedTranscript)
            .WithOne()
            .HasForeignKey<CleanedTranscript>(transcript => transcript.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        var minutes = builder.HasMany(meeting => meeting.Minutes)
            .WithOne()
            .HasForeignKey(item => item.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);
        minutes.Metadata.PrincipalToDependent!.SetField("_minutes");

        var runs = builder.HasMany(meeting => meeting.ProcessingRuns)
            .WithOne()
            .HasForeignKey(run => run.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);
        runs.Metadata.PrincipalToDependent!.SetField("_processingRuns");
    }
}

internal sealed class AudioFileConfiguration : IEntityTypeConfiguration<AudioFile>
{
    public void Configure(EntityTypeBuilder<AudioFile> builder)
    {
        builder.ToTable("AudioFiles");
        builder.HasKey(audio => audio.Id);
        builder.Property(audio => audio.Id).ValueGeneratedNever();
        builder.Property(audio => audio.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(audio => audio.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(audio => audio.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(audio => audio.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(audio => audio.MeetingId).IsUnique();
        builder.HasIndex(audio => audio.StorageKey).IsUnique();
    }
}
