using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Domain.Meetings;
using MeetingMinutesAI.Infrastructure.Persistence;

namespace MeetingMinutesAI.Infrastructure.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void DomainDoesNotReferenceOuterLayers()
    {
        var references = typeof(Meeting).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("MeetingMinutesAI.Application", references);
        Assert.DoesNotContain("MeetingMinutesAI.Infrastructure", references);
        Assert.DoesNotContain("MeetingMinutesAI.Api", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
    }

    [Fact]
    public void ApplicationOnlyReferencesDomainAmongProjectLayers()
    {
        var references = typeof(IMeetingRepository).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .Where(name => name?.StartsWith("MeetingMinutesAI.", StringComparison.Ordinal) == true)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(["MeetingMinutesAI.Domain"], references);
    }

    [Fact]
    public void InfrastructureReferencesOnlyInwardProjectLayers()
    {
        var references = typeof(MeetingMinutesDbContext).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("MeetingMinutesAI.Application", references);
        Assert.Contains("MeetingMinutesAI.Domain", references);
        Assert.DoesNotContain("MeetingMinutesAI.Api", references);
    }
}
