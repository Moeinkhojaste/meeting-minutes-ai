using MeetingMinutesAI.Domain.Meetings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeetingMinutesAI.Infrastructure.Persistence.Configurations;

internal sealed class ProcessingRunConfiguration : IEntityTypeConfiguration<ProcessingRun>
{
    public void Configure(EntityTypeBuilder<ProcessingRun> builder)
    {
        builder.ToTable("ProcessingRuns");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Id).ValueGeneratedNever();
        builder.Property(run => run.RequestedMode).HasConversion<string>().HasMaxLength(16);
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(run => run.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(run => run.ErrorCode).HasMaxLength(100);
        builder.Property(run => run.ErrorMessage).HasMaxLength(1000);
        builder.HasIndex(run => new { run.MeetingId, run.AttemptNumber }).IsUnique();
        builder.HasIndex(run => run.CorrelationId).IsUnique();

        var stages = builder.HasMany(run => run.Stages)
            .WithOne()
            .HasForeignKey(stage => stage.ProcessingRunId)
            .OnDelete(DeleteBehavior.Cascade);
        stages.Metadata.PrincipalToDependent!.SetField("_stages");
    }
}

internal sealed class ProcessingStageConfiguration : IEntityTypeConfiguration<ProcessingStage>
{
    public void Configure(EntityTypeBuilder<ProcessingStage> builder)
    {
        builder.ToTable("ProcessingStages");
        builder.HasKey(stage => stage.Id);
        builder.Property(stage => stage.Id).ValueGeneratedNever();
        builder.Property(stage => stage.Stage).HasConversion<string>().HasMaxLength(32);
        builder.Property(stage => stage.PrimaryProvider).HasMaxLength(100).IsRequired();
        builder.Property(stage => stage.PrimaryModel).HasMaxLength(200).IsRequired();
        builder.Property(stage => stage.ActualProvider).HasMaxLength(100).IsRequired();
        builder.Property(stage => stage.ActualModel).HasMaxLength(200).IsRequired();
        builder.Property(stage => stage.FallbackReason).HasMaxLength(1000);
        builder.Property(stage => stage.PromptVersion).HasMaxLength(200).IsRequired();
        builder.HasIndex(stage => new { stage.ProcessingRunId, stage.Stage }).IsUnique();
    }
}
