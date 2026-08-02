using MeetingMinutesAI.Domain.Meetings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeetingMinutesAI.Infrastructure.Persistence.Configurations;

internal sealed class MeetingMinutesConfiguration : IEntityTypeConfiguration<MeetingMinutes>
{
    public void Configure(EntityTypeBuilder<MeetingMinutes> builder)
    {
        builder.ToTable("MeetingMinutes");
        builder.HasKey(minutes => minutes.Id);
        builder.Property(minutes => minutes.Id).ValueGeneratedNever();
        builder.Property(minutes => minutes.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(minutes => minutes.Title).HasMaxLength(300);
        builder.Property(minutes => minutes.DateText).HasMaxLength(200);
        builder.Property(minutes => minutes.Summary).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(minutes => minutes.RowVersion).IsRowVersion();
        builder.HasIndex(minutes => new { minutes.MeetingId, minutes.Kind }).IsUnique();
        builder.HasOne<ProcessingRun>()
            .WithMany()
            .HasForeignKey(minutes => minutes.ProcessingRunId)
            .OnDelete(DeleteBehavior.NoAction);

        ConfigureChildren(builder.HasMany(minutes => minutes.Participants), "_participants");
        ConfigureChildren(builder.HasMany(minutes => minutes.Topics), "_topics");
        ConfigureChildren(builder.HasMany(minutes => minutes.Decisions), "_decisions");
        ConfigureChildren(builder.HasMany(minutes => minutes.ActionItems), "_actionItems");
        ConfigureChildren(builder.HasMany(minutes => minutes.OpenQuestions), "_openQuestions");
        ConfigureChildren(builder.HasMany(minutes => minutes.Uncertainties), "_uncertainties");
    }

    private static void ConfigureChildren<TChild>(
        CollectionNavigationBuilder<MeetingMinutes, TChild> navigation,
        string fieldName)
        where TChild : class
    {
        var relationship = navigation.WithOne()
            .HasForeignKey("MeetingMinutesId")
            .OnDelete(DeleteBehavior.Cascade);
        relationship.Metadata.PrincipalToDependent!.SetField(fieldName);
    }
}

internal sealed class MinutesParticipantConfiguration :
    IEntityTypeConfiguration<MinutesParticipant>
{
    public void Configure(EntityTypeBuilder<MinutesParticipant> builder)
    {
        ConfigureItem(builder, "MinutesParticipants");
        builder.Property(item => item.Name).HasMaxLength(300).IsRequired();
        var evidence = builder.HasMany(item => item.Evidence)
            .WithOne()
            .HasForeignKey(link => link.MinutesParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
        evidence.Metadata.PrincipalToDependent!.SetField("_evidence");
    }

    private static void ConfigureItem(EntityTypeBuilder<MinutesParticipant> builder, string table)
    {
        builder.ToTable(table);
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.MeetingMinutesId, item.Position }).IsUnique();
    }
}

internal sealed class MinutesTopicConfiguration : IEntityTypeConfiguration<MinutesTopic>
{
    public void Configure(EntityTypeBuilder<MinutesTopic> builder)
    {
        builder.ToTable("MinutesTopics");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Title).HasMaxLength(300).IsRequired();
        builder.Property(item => item.Summary).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(item => new { item.MeetingMinutesId, item.Position }).IsUnique();
        var evidence = builder.HasMany(item => item.Evidence)
            .WithOne()
            .HasForeignKey(link => link.MinutesTopicId)
            .OnDelete(DeleteBehavior.Cascade);
        evidence.Metadata.PrincipalToDependent!.SetField("_evidence");
    }
}

internal sealed class MinutesDecisionConfiguration : IEntityTypeConfiguration<MinutesDecision>
{
    public void Configure(EntityTypeBuilder<MinutesDecision> builder)
    {
        builder.ToTable("MinutesDecisions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Text).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(item => new { item.MeetingMinutesId, item.Position }).IsUnique();
        var evidence = builder.HasMany(item => item.Evidence)
            .WithOne()
            .HasForeignKey(link => link.MinutesDecisionId)
            .OnDelete(DeleteBehavior.Cascade);
        evidence.Metadata.PrincipalToDependent!.SetField("_evidence");
    }
}

internal sealed class MinutesActionItemConfiguration :
    IEntityTypeConfiguration<MinutesActionItem>
{
    public void Configure(EntityTypeBuilder<MinutesActionItem> builder)
    {
        builder.ToTable("MinutesActionItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Task).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(item => item.Assignee).HasMaxLength(300);
        builder.Property(item => item.Deadline).HasMaxLength(200);
        builder.HasIndex(item => new { item.MeetingMinutesId, item.Position }).IsUnique();
        var evidence = builder.HasMany(item => item.Evidence)
            .WithOne()
            .HasForeignKey(link => link.MinutesActionItemId)
            .OnDelete(DeleteBehavior.Cascade);
        evidence.Metadata.PrincipalToDependent!.SetField("_evidence");
    }
}

internal sealed class MinutesOpenQuestionConfiguration :
    IEntityTypeConfiguration<MinutesOpenQuestion>
{
    public void Configure(EntityTypeBuilder<MinutesOpenQuestion> builder)
    {
        builder.ToTable("MinutesOpenQuestions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Text).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(item => new { item.MeetingMinutesId, item.Position }).IsUnique();
        var evidence = builder.HasMany(item => item.Evidence)
            .WithOne()
            .HasForeignKey(link => link.MinutesOpenQuestionId)
            .OnDelete(DeleteBehavior.Cascade);
        evidence.Metadata.PrincipalToDependent!.SetField("_evidence");
    }
}

internal sealed class MinutesUncertaintyConfiguration :
    IEntityTypeConfiguration<MinutesUncertainty>
{
    public void Configure(EntityTypeBuilder<MinutesUncertainty> builder)
    {
        builder.ToTable("MinutesUncertainties");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Field).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Description).HasColumnType("nvarchar(max)").IsRequired();
        builder.HasIndex(item => new { item.MeetingMinutesId, item.Position }).IsUnique();
        var evidence = builder.HasMany(item => item.Evidence)
            .WithOne()
            .HasForeignKey(link => link.MinutesUncertaintyId)
            .OnDelete(DeleteBehavior.Cascade);
        evidence.Metadata.PrincipalToDependent!.SetField("_evidence");
    }
}

internal sealed class ParticipantEvidenceConfiguration :
    IEntityTypeConfiguration<ParticipantEvidence>
{
    public void Configure(EntityTypeBuilder<ParticipantEvidence> builder) =>
        EvidenceConfiguration.Configure(
            builder,
            "ParticipantEvidence",
            nameof(ParticipantEvidence.MinutesParticipantId),
            nameof(ParticipantEvidence.RawTranscriptSegmentId),
            link => link.RawSegment);
}

internal sealed class TopicEvidenceConfiguration : IEntityTypeConfiguration<TopicEvidence>
{
    public void Configure(EntityTypeBuilder<TopicEvidence> builder) =>
        EvidenceConfiguration.Configure(
            builder,
            "TopicEvidence",
            nameof(TopicEvidence.MinutesTopicId),
            nameof(TopicEvidence.RawTranscriptSegmentId),
            link => link.RawSegment);
}

internal sealed class DecisionEvidenceConfiguration : IEntityTypeConfiguration<DecisionEvidence>
{
    public void Configure(EntityTypeBuilder<DecisionEvidence> builder) =>
        EvidenceConfiguration.Configure(
            builder,
            "DecisionEvidence",
            nameof(DecisionEvidence.MinutesDecisionId),
            nameof(DecisionEvidence.RawTranscriptSegmentId),
            link => link.RawSegment);
}

internal sealed class ActionItemEvidenceConfiguration :
    IEntityTypeConfiguration<ActionItemEvidence>
{
    public void Configure(EntityTypeBuilder<ActionItemEvidence> builder) =>
        EvidenceConfiguration.Configure(
            builder,
            "ActionItemEvidence",
            nameof(ActionItemEvidence.MinutesActionItemId),
            nameof(ActionItemEvidence.RawTranscriptSegmentId),
            link => link.RawSegment);
}

internal sealed class OpenQuestionEvidenceConfiguration :
    IEntityTypeConfiguration<OpenQuestionEvidence>
{
    public void Configure(EntityTypeBuilder<OpenQuestionEvidence> builder) =>
        EvidenceConfiguration.Configure(
            builder,
            "OpenQuestionEvidence",
            nameof(OpenQuestionEvidence.MinutesOpenQuestionId),
            nameof(OpenQuestionEvidence.RawTranscriptSegmentId),
            link => link.RawSegment);
}

internal sealed class UncertaintyEvidenceConfiguration :
    IEntityTypeConfiguration<UncertaintyEvidence>
{
    public void Configure(EntityTypeBuilder<UncertaintyEvidence> builder) =>
        EvidenceConfiguration.Configure(
            builder,
            "UncertaintyEvidence",
            nameof(UncertaintyEvidence.MinutesUncertaintyId),
            nameof(UncertaintyEvidence.RawTranscriptSegmentId),
            link => link.RawSegment);
}
