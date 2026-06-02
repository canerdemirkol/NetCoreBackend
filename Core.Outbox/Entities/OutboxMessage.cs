using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Outbox.Entities;

// One row per integration event waiting to ship. Written in the same SaveChanges as the
// business change it describes, so the database commit IS the publish-intent — there's no
// window where the row exists but the event was never enqueued.
//
// The worker reads OccurredAtUtc-ordered, picks up rows where ProcessedAtUtc is null and
// the next attempt time has arrived, hands them to the consumer-supplied IOutboxPublisher,
// and stamps ProcessedAtUtc on success or schedules a retry on failure.
//
// Tenant-aware: inherits TenantEntity so per-tenant outbox routing / archival is possible.
public class OutboxMessage : TenantEntity<Guid>
{
    // Type marker the consumer uses to route the event (e.g. "Orders.Placed.v1"). Versioning
    // is the consumer's responsibility — the framework treats this as an opaque string.
    public string EventType { get; set; } = string.Empty;

    // Serialized payload. System.Text.Json is the default convention; a consumer is free to
    // store another format as long as IOutboxPublisher knows how to read it.
    public string Payload { get; set; } = string.Empty;

    // Correlation / trace id propagated from the originating request, so downstream consumers
    // can stitch events back to the user action that produced them.
    public string? CorrelationId { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    // null while pending; stamped on successful publish.
    public DateTime? ProcessedAtUtc { get; set; }

    // Retry bookkeeping. When the publisher throws, the worker bumps AttemptCount and sets
    // NextAttemptUtc using ExponentialBackoff. After MaxAttempts the row is marked poisoned
    // (ProcessedAtUtc remains null, NextAttemptUtc set far in the future) and surfaced to
    // operators via Error.
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptUtc { get; set; }
    public string? Error { get; set; }
    public bool IsPoisoned { get; set; }
}
