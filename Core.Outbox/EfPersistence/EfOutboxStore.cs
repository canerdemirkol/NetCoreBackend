using Microsoft.EntityFrameworkCore;
using NetCoreBackend.NArchitecture.Core.Outbox.Abstractions;
using NetCoreBackend.NArchitecture.Core.Outbox.Entities;
using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Outbox.EfPersistence;

// EF Core-backed outbox store. Generic over the consumer's DbContext so the consumer keeps
// ownership of the connection / migration story; this library only needs a DbSet<OutboxMessage>.
//
// Important contract: the consumer's DbContext MUST expose `DbSet<OutboxMessage> OutboxMessages`
// or configure the entity manually. See EfOutboxModelExtensions for the recommended
// OnModelCreating call.
//
// Isolation contract: the methods that call SaveChangesAsync (MarkProcessed / RecordFailure)
// will flush EVERY tracked change on the DbContext, not just the outbox row. The shipped
// OutboxPublisherWorker opens a fresh DI scope per batch so this is safe by construction.
// Consumers using EfOutboxStore from a request-scoped DbContext must be aware they are
// flushing on the worker's behalf and avoid leaving stray tracked entities that should not
// commit yet — typically by using a dedicated DbContext scope around the outbox call.
public sealed class EfOutboxStore<TDbContext> : IOutboxStore where TDbContext : DbContext
{
    private readonly TDbContext _context;
    private readonly ITenantEntitySetter? _tenantSetter;

    // tenantSetter is optional: a non-multi-tenant app can use the outbox without taking
    // Core.MultiTenancy as a dependency. When ITenantEntitySetter IS registered, AppendAsync
    // stamps the row's TenantId from the current context (mirroring EfRepositoryBase's
    // EditEntityPropertiesToAdd behavior). When it's missing AND the message has a default
    // TenantId, AppendAsync fails fast so the row never lands with Guid.Empty — which would
    // otherwise be invisible to every tenant filter.
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
