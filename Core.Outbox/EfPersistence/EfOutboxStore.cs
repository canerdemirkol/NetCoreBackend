using Microsoft.EntityFrameworkCore;
using NetCoreBackend.NArchitecture.Core.Outbox.Abstractions;
using NetCoreBackend.NArchitecture.Core.Outbox.Entities;

namespace NetCoreBackend.NArchitecture.Core.Outbox.EfPersistence;

// EF Core-backed outbox store. Generic over the consumer's DbContext so the consumer keeps
// ownership of the connection / migration story; this library only needs a DbSet<OutboxMessage>.
//
// Important contract: the consumer's DbContext MUST expose `DbSet<OutboxMessage> OutboxMessages`
// or configure the entity manually. See EfOutboxModelExtensions for the recommended
// OnModelCreating call.
public sealed class EfOutboxStore<TDbContext> : IOutboxStore where TDbContext : DbContext
{
    private readonly TDbContext _context;

    public EfOutboxStore(TDbContext context) => _context = context;

    public Task AppendAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.OccurredAtUtc == default)
            message.OccurredAtUtc = DateTime.UtcNow;
        _context.Set<OutboxMessage>().Add(message);
        // Caller commits via their normal SaveChangesAsync so the row is atomic with the
        // surrounding transaction. AppendAsync deliberately does NOT save here.
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<OutboxMessage>> FetchDueAsync(int batchSize, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        return await _context.Set<OutboxMessage>()
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
