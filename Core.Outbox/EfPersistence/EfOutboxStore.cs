using Microsoft.EntityFrameworkCore;
using NetCoreBackend.NArchitecture.Core.Outbox.Abstractions;
using NetCoreBackend.NArchitecture.Core.Outbox.Entities;
using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Outbox.EfPersistence;

/// <summary>
/// EF Core-backed <see cref="IOutboxStore"/> generic over the consumer's <c>DbContext</c>.
/// The library only needs a <c>DbSet&lt;OutboxMessage&gt;</c>; the consumer keeps ownership
/// of connection setup, migrations and the schema.
/// </summary>
/// <typeparam name="TDbContext">Concrete <c>DbContext</c> that holds the outbox table.</typeparam>
/// <remarks>
/// <para>
/// <strong>Schema contract:</strong> the consumer's <c>OnModelCreating</c> MUST call
/// <see cref="EfOutboxModelExtensions.ConfigureOutbox"/> (or define <c>OutboxMessage</c>
/// manually with equivalent indexes).
/// </para>
/// <para>
/// <strong>Tenant contract:</strong> when <c>ITenantEntitySetter</c> is registered,
/// <see cref="AppendAsync"/> calls <c>SetTenantId</c> unconditionally — for a regular
/// tenant user the active tenant overrides any caller-provided <c>TenantId</c>, blocking
/// cross-tenant write attempts; for SuperAdmin without impersonation the caller MUST
/// pre-set <c>TenantId</c>. When no setter is registered AND <c>TenantId</c> is empty,
/// <see cref="AppendAsync"/> throws to prevent <c>Guid.Empty</c> rows that would be
/// invisible to every tenant filter.
/// </para>
/// <para>
/// <strong>Isolation contract:</strong> <see cref="MarkProcessedAsync"/> and
/// <see cref="RecordFailureAsync"/> call <c>SaveChangesAsync</c>, which flushes EVERY
/// tracked change on the <c>DbContext</c>. The shipped <c>OutboxPublisherWorker</c> opens
/// a fresh DI scope per batch so this is safe by construction. Consumers reusing the store
/// from a request-scoped <c>DbContext</c> must avoid leaving stray tracked entities that
/// should not commit yet.
/// </para>
/// </remarks>
public sealed class EfOutboxStore<TDbContext> : IOutboxStore where TDbContext : DbContext
{
    private readonly TDbContext _context;
    private readonly ITenantEntitySetter? _tenantSetter;

    /// <summary>
    /// Construct the store. <paramref name="tenantSetter"/> is optional: non-multi-tenant
    /// apps can use the outbox without taking <c>Core.MultiTenancy</c> as a dependency.
    /// </summary>
    public EfOutboxStore(TDbContext context, ITenantEntitySetter? tenantSetter = null)
    {
        _context = context;
        _tenantSetter = tenantSetter;
    }

    public Task AppendAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.OccurredAtUtc == default)
            message.OccurredAtUtc = DateTime.UtcNow;

        // Mirrors EfRepositoryBase.EditEntityPropertiesToAdd / TenantEntitySetter.SetTenantId:
        //   - Tenant user (CurrentTenantId.HasValue): ALWAYS overwrite message.TenantId with
        //     the context's tenant. This deliberately ignores any caller-provided TenantId so
        //     a tenant-A user cannot construct a message with TenantId=tenantB and slip a
        //     cross-tenant write past the framework.
        //   - SuperAdmin without impersonation (CurrentTenantId is null but IsSuperAdmin):
        //     caller MUST pre-set TenantId; SetTenantId throws otherwise.
        //   - No tenant context AND not SuperAdmin: SetTenantId throws (no orphan rows).
        //   - No ITenantEntitySetter wired at all: caller is responsible — fail loud if the
        //     row would land with Guid.Empty (invisible to every tenant filter).
        if (_tenantSetter is null)
        {
            if (message.TenantId == Guid.Empty)
                throw new InvalidOperationException(
                    "Cannot append OutboxMessage: TenantId is empty and ITenantEntitySetter is not registered. " +
                    "Either set message.TenantId explicitly or call services.AddMultiTenancy() so the current " +
                    "tenant can be stamped automatically.");
        }
        else
        {
            _tenantSetter.SetTenantId(message);
        }

        _context.Set<OutboxMessage>().Add(message);
        // Caller commits via their normal SaveChangesAsync so the row is atomic with the
        // surrounding transaction. AppendAsync deliberately does NOT save here.
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<OutboxMessage>> FetchDueAsync(int batchSize, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        // IgnoreQueryFilters: the worker is cross-tenant by design — it must drain rows
        // for ALL tenants. Consumers that auto-apply a tenant filter to every ITenantEntity
        // (a common OnModelCreating loop) would otherwise see zero rows here because the
        // BackgroundService scope has no tenant context (no HttpContext, no middleware).
        // TenantId is preserved on each row, so downstream publishers can route per-tenant
        // if they need to.
        return await _context.Set<OutboxMessage>()
            .IgnoreQueryFilters()
            .Where(m => !m.IsPoisoned
                        && m.ProcessedAtUtc == null
                        && (m.NextAttemptUtc == null || m.NextAttemptUtc <= now))
            .OrderBy(m => m.OccurredAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MarkProcessedAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        message.ProcessedAtUtc = DateTime.UtcNow;
        message.Error = null;
        message.NextAttemptUtc = null;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordFailureAsync(OutboxMessage message, string error, DateTime? nextAttemptUtc, bool poisoned, CancellationToken cancellationToken)
    {
        message.AttemptCount += 1;
        message.Error = error;
        message.NextAttemptUtc = nextAttemptUtc;
        message.IsPoisoned = poisoned;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
