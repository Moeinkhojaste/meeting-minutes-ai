using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Application.Abstractions.Storage;
using MeetingMinutesAI.Application.Meetings;
using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Application.Tests;

public sealed class MeetingServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateTrimsTitleAndUsesUtcClock()
    {
        var state = CreateService();

        var result = await state.Service.CreateAsync("  Planning  ");

        Assert.Equal("Planning", result.Title);
        Assert.Equal(Now, result.CreatedAt);
        Assert.Equal(MeetingProcessingStatus.Created, result.Status);
        Assert.Equal(1, state.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ListRequestsTheExpectedPage()
    {
        var meetings = new[]
        {
            Meeting.Create("Third", Now.AddMinutes(2)),
            Meeting.Create("Second", Now.AddMinutes(1)),
            Meeting.Create("First", Now),
        };
        var state = CreateService(meetings);

        var result = await state.Service.ListAsync(2, 2);

        Assert.Equal(2, state.Repository.LastSkip);
        Assert.Equal(2, state.Repository.LastTake);
        Assert.Equal(2, result.Page);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task ListReturnsEmptyForAValidPageBeyondTheResultSet()
    {
        var state = CreateService([Meeting.Create("Only", Now)]);

        var result = await state.Service.ListAsync(int.MaxValue, 100);

        Assert.Empty(result.Items);
        Assert.Equal(0, state.Repository.LastTake);
    }

    [Fact]
    public async Task MissingMeetingReturnsNotFound()
    {
        var state = CreateService();

        await Assert.ThrowsAsync<MeetingNotFoundException>(() =>
            state.Service.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateRejectsAStaleVersion()
    {
        var meeting = Meeting.Create("Original", Now);
        var state = CreateService([meeting]);

        await Assert.ThrowsAsync<MeetingConcurrencyException>(() =>
            state.Service.UpdateAsync(meeting.Id, "Changed", [1]));

        Assert.Equal("Original", meeting.Title);
        Assert.Equal(0, state.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateRenamesAndClearsTitle()
    {
        var meeting = Meeting.Create("Original", Now);
        var state = CreateService([meeting]);

        var renamed = await state.Service.UpdateAsync(meeting.Id, "  Changed  ", []);
        var cleared = await state.Service.UpdateAsync(meeting.Id, null, []);

        Assert.Equal("Changed", renamed.Title);
        Assert.Null(cleared.Title);
        Assert.Equal(2, state.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task DeleteRejectsQueuedMeeting()
    {
        var meeting = Meeting.Create("Active", Now);
        meeting.AttachAudio(
            "meeting.wav",
            "audio/active",
            "audio/wav",
            10,
            100,
            new string('a', 64),
            Now);
        meeting.Queue(Now);
        var state = CreateService([meeting]);

        await Assert.ThrowsAsync<MeetingActiveProcessingException>(() =>
            state.Service.DeleteAsync(meeting.Id, []));

        Assert.False(state.Repository.WasRemoved);
    }

    [Fact]
    public async Task SaveTimeRaceBecomesMeetingConcurrencyConflict()
    {
        var meeting = Meeting.Create("Original", Now);
        var state = CreateService([meeting]);
        state.UnitOfWork.ThrowConcurrency = true;

        await Assert.ThrowsAsync<MeetingConcurrencyException>(() =>
            state.Service.UpdateAsync(meeting.Id, "Changed", []));
    }

    [Fact]
    public async Task DeleteCommitsStagedAudioRemovalAfterDatabaseSuccess()
    {
        var meeting = Meeting.Create("Uploaded", Now);
        meeting.AttachAudio("meeting.wav", "audio/key", "audio/wav", 10, null,
            new string('a', 64), Now);
        var state = CreateService([meeting]);

        await state.Service.DeleteAsync(meeting.Id, []);

        Assert.True(state.Storage.StageCalled);
        Assert.True(state.Storage.CommitCalled);
        Assert.False(state.Storage.RollbackCalled);
    }

    [Fact]
    public async Task DeleteRestoresStagedAudioWhenDatabaseSaveFails()
    {
        var meeting = Meeting.Create("Uploaded", Now);
        meeting.AttachAudio("meeting.wav", "audio/key", "audio/wav", 10, null,
            new string('a', 64), Now);
        var state = CreateService([meeting]);
        state.UnitOfWork.ThrowConcurrency = true;

        await Assert.ThrowsAsync<MeetingConcurrencyException>(() =>
            state.Service.DeleteAsync(meeting.Id, []));

        Assert.True(state.Storage.StageCalled);
        Assert.False(state.Storage.CommitCalled);
        Assert.True(state.Storage.RollbackCalled);
    }

    private static TestState CreateService(IReadOnlyList<Meeting>? meetings = null)
    {
        var repository = new FakeMeetingRepository(meetings ?? []);
        var unitOfWork = new FakeUnitOfWork();
        var storage = new FakeAudioStorage();
        var service = new MeetingService(
            repository,
            unitOfWork,
            storage,
            new FixedTimeProvider(Now));
        return new TestState(service, repository, unitOfWork, storage);
    }

    private sealed record TestState(
        MeetingService Service,
        FakeMeetingRepository Repository,
        FakeUnitOfWork UnitOfWork,
        FakeAudioStorage Storage);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeMeetingRepository : IMeetingRepository
    {
        private readonly List<Meeting> _meetings;

        public FakeMeetingRepository(IReadOnlyList<Meeting> meetings)
        {
            _meetings = [.. meetings];
        }

        public int LastSkip { get; private set; }
        public int LastTake { get; private set; }
        public bool WasRemoved { get; private set; }

        public Task<Meeting?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_meetings.SingleOrDefault(meeting => meeting.Id == id));

        public Task<IReadOnlyList<Meeting>> ListAsync(
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            LastSkip = skip;
            LastTake = take;
            return Task.FromResult<IReadOnlyList<Meeting>>(
                _meetings.Skip(skip).Take(take).ToList());
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_meetings.Count);

        public Task AddAsync(
            Meeting meeting,
            CancellationToken cancellationToken = default)
        {
            _meetings.Add(meeting);
            return Task.CompletedTask;
        }

        public void Remove(Meeting meeting)
        {
            WasRemoved = true;
            _meetings.Remove(meeting);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public bool ThrowConcurrency { get; set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (ThrowConcurrency)
            {
                throw new PersistenceConcurrencyException(new Exception("test"));
            }

            return Task.FromResult(1);
        }
    }

    private sealed class FakeAudioStorage : IAudioStorage
    {
        public bool StageCalled { get; private set; }
        public bool CommitCalled { get; private set; }
        public bool RollbackCalled { get; private set; }

        public Task<StoredAudio> SaveAsync(Stream source, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IStagedAudioDeletion> StageDeleteAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            StageCalled = true;
            return Task.FromResult<IStagedAudioDeletion>(new FakeStagedDeletion(this));
        }

        private sealed class FakeStagedDeletion(FakeAudioStorage owner) : IStagedAudioDeletion
        {
            private bool _committed;

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                _committed = true;
                owner.CommitCalled = true;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                if (!_committed)
                {
                    owner.RollbackCalled = true;
                }
                return ValueTask.CompletedTask;
            }
        }
    }
}
