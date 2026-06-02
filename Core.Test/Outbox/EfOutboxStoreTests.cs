using Microsoft.EntityFrameworkCore;
using Moq;
using NetCoreBackend.NArchitecture.Core.Outbox.EfPersistence;
using NetCoreBackend.NArchitecture.Core.Outbox.Entities;
using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Test.Outbox;

public sealed class EfOutboxStoreTests
{
    private static readonly Guid TenantA = Guid.NewGuid();

    private sealed class TestDb : DbContext
    {
        public TestDb(DbContextOptions<TestDb> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb) => mb.ConfigureOutbox();
    }

    private static TestDb NewDb() =>
        new(new DbContextOptionsBuilder<TestDb>()
            .UseInMemoryDatabase($"outbox-{Guid.NewGuid()}")
            .Options);

    // Test-bound store: TenantId is supplied directly on the message so we don't need to
    // stand up a real ITenantEntitySetter / TenantContext for every test. The store still
    // enforces "TenantId must be non-empty before Add"; that's the contract being verified.
    private static OutboxMessage NewMsg(string type, DateTime occurred, Guid? tenantId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? TenantA,
            EventType = type,
            Payload = "{}",
            OccurredAtUtc = occurred
        };

    [Fact]
    public async Task FetchDue_ReturnsOnlyPendingNonPoisonedAndReadyToRetry()
    {
        await using TestDb ctx = NewDb();
        EfOutboxStore<TestDb> store = new(ctx);

        OutboxMessage processed = NewMsg("A", DateTime.UtcNow.AddMinutes(-5));
        processed.ProcessedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        OutboxMessage poisoned = NewMsg("B", DateTime.UtcNow.AddMinutes(-4));
        poisoned.IsPoisoned = true;
        OutboxMessage future = NewMsg("C", DateTime.UtcNow.AddMinutes(-3));
        future.NextAttemptUtc = DateTime.UtcNow.AddMinutes(10);
        OutboxMessage dueNow = NewMsg("D", DateTime.UtcNow.AddMinutes(-2));
        OutboxMessage dueRetry = NewMsg("E", DateTime.UtcNow.AddMinutes(-1));
        dueRetry.NextAttemptUtc = DateTime.UtcNow.AddSeconds(-5);

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

        await store.AppendAsync(NewMsg("X", DateTime.UtcNow), CancellationToken.None);

        // Verify nothing persisted yet by reading via AsNoTracking against the same context —
        // in-memory shares state via DbContextOptions name so this exercises the actual
        // "Add does not Save" semantic.
        Assert.Empty(await ctx.Set<OutboxMessage>().AsNoTracking().ToListAsync());

        await ctx.SaveChangesAsync();
        Assert.Single(await ctx.Set<OutboxMessage>().AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AppendAsync_EmptyTenantIdWithoutSetter_Throws()
    {
        await using TestDb ctx = NewDb();
        EfOutboxStore<TestDb> store = new(ctx);

        OutboxMessage withoutTenant = new() { Id = Guid.NewGuid(), EventType = "X", Payload = "{}" };

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.AppendAsync(withoutTenant, CancellationToken.None));

        Assert.Contains("TenantId is empty", ex.Message);
    }

    [Fact]
    public async Task AppendAsync_EmptyTenantIdWithSetter_StampsFromContext()
    {
        await using TestDb ctx = NewDb();
        Guid expectedTenant = Guid.NewGuid();

        Mock<ITenantEntitySetter> setter = new();
        setter.SetupGet(s => s.CurrentTenantId).Returns(expectedTenant);
        setter.SetupGet(s => s.IsSuperAdmin).Returns(false);
        setter.Setup(s => s.SetTenantId(It.IsAny<ITenantEntity>()))
            .Callback<ITenantEntity>(e => e.TenantId = expectedTenant);

        EfOutboxStore<TestDb> store = new(ctx, setter.Object);

        OutboxMessage message = new() { Id = Guid.NewGuid(), EventType = "X", Payload = "{}" };
        await store.AppendAsync(message, CancellationToken.None);

        Assert.Equal(expectedTenant, message.TenantId);
        setter.Verify(s => s.SetTenantId(message), Times.Once);
    }

    [Fact]
    public async Task AppendAsync_CallerProvidedTenantId_OverridenByActiveTenantContext()
    {
        // Regression for R4-FIX #1c: a tenant-A user must not be able to write a row with
        // TenantId=tenantB by pre-setting it on the message. SetTenantId from the active
        // context ALWAYS overrides caller-provided values for regular tenant users.
        await using TestDb ctx = NewDb();
        Guid contextTenant = Guid.NewGuid();
        Guid foreignTenant = Guid.NewGuid();

        Mock<ITenantEntitySetter> setter = new();
        setter.SetupGet(s => s.CurrentTenantId).Returns(contextTenant);
        setter.SetupGet(s => s.IsSuperAdmin).Returns(false);
        setter.Setup(s => s.SetTenantId(It.IsAny<ITenantEntity>()))
            .Callback<ITenantEntity>(e => e.TenantId = contextTenant);

        EfOutboxStore<TestDb> store = new(ctx, setter.Object);

        OutboxMessage hostile = new() { Id = Guid.NewGuid(), TenantId = foreignTenant, EventType = "X", Payload = "{}" };
        await store.AppendAsync(hostile, CancellationToken.None);

        Assert.Equal(contextTenant, hostile.TenantId);
        Assert.NotEqual(foreignTenant, hostile.TenantId);
    }

    [Fact]
    public async Task RecordFailure_BumpsAttemptCount_AndPoisons()
    {
        await using TestDb ctx = NewDb();
        EfOutboxStore<TestDb> store = new(ctx);

        OutboxMessage m = NewMsg("Z", DateTime.UtcNow);
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
