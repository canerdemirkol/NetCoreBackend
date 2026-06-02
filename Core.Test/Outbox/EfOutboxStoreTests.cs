using Microsoft.EntityFrameworkCore;
using NetCoreBackend.NArchitecture.Core.Outbox.EfPersistence;
using NetCoreBackend.NArchitecture.Core.Outbox.Entities;

namespace NetCoreBackend.NArchitecture.Core.Test.Outbox;

public sealed class EfOutboxStoreTests
{
    private sealed class TestDb : DbContext
    {
        public TestDb(DbContextOptions<TestDb> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb) => mb.ConfigureOutbox();
    }

    private static TestDb NewDb() =>
        new(new DbContextOptionsBuilder<TestDb>()
            .UseInMemoryDatabase($"outbox-{Guid.NewGuid()}")
            .Options);

    [Fact]
    public async Task FetchDue_ReturnsOnlyPendingNonPoisonedAndReadyToRetry()
    {
        await using TestDb ctx = NewDb();
        EfOutboxStore<TestDb> store = new(ctx);

        OutboxMessage processed = new() { Id = Guid.NewGuid(), EventType = "A", Payload = "{}", OccurredAtUtc = DateTime.UtcNow.AddMinutes(-5), ProcessedAtUtc = DateTime.UtcNow.AddMinutes(-1) };
        OutboxMessage poisoned = new() { Id = Guid.NewGuid(), EventType = "B", Payload = "{}", OccurredAtUtc = DateTime.UtcNow.AddMinutes(-4), IsPoisoned = true };
        OutboxMessage future = new() { Id = Guid.NewGuid(), EventType = "C", Payload = "{}", OccurredAtUtc = DateTime.UtcNow.AddMinutes(-3), NextAttemptUtc = DateTime.UtcNow.AddMinutes(10) };
        OutboxMessage dueNow = new() { Id = Guid.NewGuid(), EventType = "D", Payload = "{}", OccurredAtUtc = DateTime.UtcNow.AddMinutes(-2) };
        OutboxMessage dueRetry = new() { Id = Guid.NewGuid(), EventType = "E", Payload = "{}", OccurredAtUtc = DateTime.UtcNow.AddMinutes(-1), NextAttemptUtc = DateTime.UtcNow.AddSeconds(-5) };

        ctx.AddRange(processed, poisoned, future, dueNow, dueRetry);
        await ctx.SaveChangesAsync();

        IReadOnlyList<OutboxMessage> result = await store.FetchDueAsync(10, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("D", result[0].EventType);   // ordered by OccurredAtUtc ascending
        Assert.Equal("E", result[1].EventType);
    }

    [Fact]
    public async Task AppendAsync_DoesNotSave_UntilCallerCommits()
    {
        await using TestDb ctx = NewDb();
        EfOutboxStore<TestDb> store = new(ctx);

        await store.AppendAsync(new OutboxMessage { Id = Guid.NewGuid(), EventType = "X", Payload = "{}" }, CancellationToken.None);

        // Verify nothing persisted yet by opening a fresh context against the same DB name —
        // but in-memory shares state via DbContextOptions name, so we re-use ctx and check
        // ChangeTracker before SaveChanges.
        Assert.Empty(await ctx.Set<OutboxMessage>().AsNoTracking().ToListAsync());

        await ctx.SaveChangesAsync();
        Assert.Single(await ctx.Set<OutboxMessage>().AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task RecordFailure_BumpsAttemptCount_AndPoisons()
    {
        await using TestDb ctx = NewDb();
        EfOutboxStore<TestDb> store = new(ctx);

        OutboxMessage m = new() { Id = Guid.NewGuid(), EventType = "Z", Payload = "{}", OccurredAtUtc = DateTime.UtcNow };
        await store.AppendAsync(m, CancellationToken.None);
        await ctx.SaveChangesAsync();

        await store.RecordFailureAsync(m, "boom", DateTime.UtcNow.AddSeconds(2), poisoned: false, CancellationToken.None);
        Assert.Equal(1, m.AttemptCount);
        Assert.False(m.IsPoisoned);
        Assert.Equal("boom", m.Error);

        await store.RecordFailureAsync(m, "still boom", null, poisoned: true, CancellationToken.None);
        Assert.Equal(2, m.AttemptCount);
        Assert.True(m.IsPoisoned);
    }
}
