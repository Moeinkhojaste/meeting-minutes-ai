using MeetingMinutesAI.Domain.Meetings;
using MeetingMinutesAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MeetingMinutesAI.Infrastructure.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void ModelContainsTheNormalizedMeetingAggregate()
    {
        using var context = CreateSqlServerContext();
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(Meeting)));
        Assert.NotNull(model.FindEntityType(typeof(AudioFile)));
        Assert.NotNull(model.FindEntityType(typeof(RawTranscriptSegment)));
        Assert.NotNull(model.FindEntityType(typeof(CleanedSegmentSource)));
        Assert.NotNull(model.FindEntityType(typeof(MeetingMinutes)));
        Assert.NotNull(model.FindEntityType(typeof(MinutesActionItem)));
        Assert.NotNull(model.FindEntityType(typeof(ProcessingRun)));
        Assert.NotNull(model.FindEntityType(typeof(ProcessingStage)));
    }

    [Theory]
    [InlineData(typeof(Meeting))]
    [InlineData(typeof(RawTranscript))]
    [InlineData(typeof(CleanedTranscript))]
    [InlineData(typeof(MeetingMinutes))]
    public void EditableRootsUseRowVersionConcurrency(Type entityType)
    {
        using var context = CreateSqlServerContext();
        var property = context.Model.FindEntityType(entityType)!
            .FindProperty("RowVersion")!;

        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    [Fact]
    public void EnumsAreStoredAsStrings()
    {
        using var context = CreateSqlServerContext();
        var status = context.Model.FindEntityType(typeof(Meeting))!
            .FindProperty(nameof(Meeting.Status))!;
        var kind = context.Model.FindEntityType(typeof(MeetingMinutes))!
            .FindProperty(nameof(MeetingMinutes.Kind))!;

        Assert.Equal(typeof(string), status.GetProviderClrType());
        Assert.Equal(typeof(string), kind.GetProviderClrType());
    }

    [Fact]
    public void CrossGraphEvidenceDoesNotCascadeFromRawSegment()
    {
        using var context = CreateSqlServerContext();
        var entity = context.Model.FindEntityType(typeof(ParticipantEvidence))!;
        var foreignKey = entity.GetForeignKeys().Single(key =>
            key.PrincipalEntityType.ClrType == typeof(RawTranscriptSegment));

        Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void MeetingMinutesKindAndSegmentIdentifiersAreUniqueWithinParents()
    {
        using var context = CreateSqlServerContext();
        var minutesIndexes = context.Model.FindEntityType(typeof(MeetingMinutes))!.GetIndexes();
        var segmentIndexes = context.Model.FindEntityType(typeof(RawTranscriptSegment))!.GetIndexes();

        Assert.Contains(minutesIndexes, index => index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(MeetingMinutes.MeetingId), nameof(MeetingMinutes.Kind)]));
        Assert.Contains(segmentIndexes, index => index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(RawTranscriptSegment.RawTranscriptId),
                    nameof(RawTranscriptSegment.ExternalId),
                ]));
    }

    private static MeetingMinutesDbContext CreateSqlServerContext()
    {
        var options = new DbContextOptionsBuilder<MeetingMinutesDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ModelOnly;Trusted_Connection=True")
            .Options;
        return new MeetingMinutesDbContext(options);
    }
}
