using System.Linq.Expressions;
using MeetingMinutesAI.Domain.Meetings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MeetingMinutesAI.Infrastructure.Persistence.Configurations;

internal static class EvidenceConfiguration
{
    public static void Configure<TLink>(
        EntityTypeBuilder<TLink> builder,
        string tableName,
        string itemId,
        string rawSegmentId,
        Expression<Func<TLink, RawTranscriptSegment?>> rawSegment)
        where TLink : class
    {
        builder.ToTable(tableName);
        builder.HasKey(itemId, rawSegmentId);
        builder.HasOne(rawSegment)
            .WithMany()
            .HasForeignKey(rawSegmentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
