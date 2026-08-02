using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Domain.Meetings;
using MeetingMinutesAI.Infrastructure;
using MeetingMinutesAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeetingMinutesAI.Infrastructure.Tests;

public sealed class RepositoryTests
{
    [Fact]
    public async Task RepositoryAddsLoadsAndRemovesAggregate()
    {
        var databaseName = $"repository-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<MeetingMinutesDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var now = DateTimeOffset.UtcNow;
        var meeting = Meeting.Create("Test", now);
        meeting.AttachAudio(
            "meeting.wav",
            "audio/test-key",
            "audio/wav",
            128,
            1000,
            new string('b', 64),
            now);

        await using (var writeContext = new MeetingMinutesDbContext(options))
        {
            var repository = new MeetingRepository(writeContext);
            await repository.AddAsync(meeting);
            await writeContext.SaveChangesAsync();
        }

        await using (var readContext = new MeetingMinutesDbContext(options))
        {
            var repository = new MeetingRepository(readContext);
            var loaded = await repository.GetByIdAsync(meeting.Id);
            Assert.NotNull(loaded);
            Assert.Equal("audio/test-key", loaded.AudioFile!.StorageKey);

            repository.Remove(loaded);
            await readContext.SaveChangesAsync();
        }

        await using var verificationContext = new MeetingMinutesDbContext(options);
        Assert.False(await verificationContext.Meetings.AnyAsync());
    }

    [Fact]
    public void UnitOfWorkAndContextShareTheSameScope()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=ScopeOnly;Trusted_Connection=True",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<MeetingMinutesDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        Assert.Same(context, unitOfWork);
    }

    [Fact]
    public void InfrastructureRequiresAConnectionString()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration));

        Assert.Equal("Connection string 'DefaultConnection' is required.", exception.Message);
    }
}
