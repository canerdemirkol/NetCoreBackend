using NetCoreBackend.NArchitecture.Core.Outbox.Entities;

namespace NetCoreBackend.NArchitecture.Core.Outbox.Abstractions;

// Storage abstraction for the outbox. The default EfOutboxStore (in this library) uses a
// DbContext-resolved DbSet<OutboxMessage>; a consumer can replace it with a non-EF backing
// store if they choose.
public interface IOutboxStore
{
    // Atomically append a message in the same SaveChanges() as the business write — caller
    // is responsible for invoking SaveChanges (typically via TransactionScopeBehavior).
    Task AppendAsync(OutboxMessage message, CancellationToken cancellationToken);

    // Pull up to `batchSize` rows that are due for dispatch (not processed, not poisoned,
    // NextAttemptUtc is null or in the past). Ordered by OccurredAtUtc ascending so older
    // events ship first.
    Task<IReadOnlyList<OutboxMessage>> FetchDueAsync(int batchSize, CancellationToken cancellationToken);

    // Mark as successfully delivered. The store is expected to persist this immediately.
    Task MarkProcessedAsync(OutboxMessage message, CancellationToken cancellationToken);

    // Record a failure. After MaxAttempts the store marks the row poisoned so the worker
    // stops picking it up.
    Task RecordFailureAsync(OutboxMessage message, string error, DateTime? nextAttemptUtc, bool poisoned, CancellationToken cancellationToken);
}
