using NetCoreBackend.NArchitecture.Core.Outbox.Entities;

namespace NetCoreBackend.NArchitecture.Core.Outbox.Abstractions;

/// <summary>
/// Storage abstraction for the outbox. The default <c>EfOutboxStore&lt;TDbContext&gt;</c>
/// shipped with the framework uses the consumer's <c>DbContext</c>; replace this
/// registration to back the outbox with a non-EF store (Cosmos, Redis Streams, …).
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Append a message to the outbox. The store DOES NOT commit — caller flushes via the
    /// surrounding <c>SaveChangesAsync</c> so the row is atomic with the business write.
    /// </summary>
    /// <remarks>
    /// Tenant stamp: when <c>message.TenantId</c> is empty, <c>EfOutboxStore</c> consults
    /// the registered <c>ITenantEntitySetter</c> and stamps from the current context (or
    /// throws if no context is available). When <c>TenantId</c> is already populated for a
    /// regular tenant user, <c>SetTenantId</c> overrides it with the active tenant to
    /// block cross-tenant write attempts.
    /// </remarks>
    Task AppendAsync(OutboxMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Pull up to <paramref name="batchSize"/> rows that are due for dispatch
    /// (not processed, not poisoned, <c>NextAttemptUtc</c> is <c>null</c> or in the past),
    /// ordered by <c>OccurredAtUtc</c> ascending so older events ship first.
    /// </summary>
    /// <remarks>
    /// <c>EfOutboxStore</c> calls <c>IgnoreQueryFilters()</c> here so the worker — which
    /// runs without a tenant context — drains rows for ALL tenants.
    /// </remarks>
    Task<IReadOnlyList<OutboxMessage>> FetchDueAsync(int batchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Mark the message as successfully delivered and persist immediately.
    /// </summary>
    Task MarkProcessedAsync(OutboxMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Record a publish failure: bump <c>AttemptCount</c>, set <c>NextAttemptUtc</c> to the
    /// caller-computed backoff, and (when <paramref name="poisoned"/> is <c>true</c>) flag
    /// the row so the worker stops picking it up. Persisted immediately.
    /// </summary>
    Task RecordFailureAsync(OutboxMessage message, string error, DateTime? nextAttemptUtc, bool poisoned, CancellationToken cancellationToken);
}
