using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Outbox.Entities;

/// <summary>
/// One row per integration event waiting to ship. Written in the same SaveChanges as the
/// business change it describes, so the database commit IS the publish-intent — there's no
/// window where the row exists but the event was never enqueued.
/// </summary>
/// <remarks>
/// <para>
/// The worker reads rows ordered by <see cref="OccurredAtUtc"/>, picks up entries where
/// <see cref="ProcessedAtUtc"/> is <c>null</c> and the next attempt time has arrived, hands
/// them to the consumer-supplied <c>IOutboxPublisher</c>, and stamps
/// <see cref="ProcessedAtUtc"/> on success or schedules a retry on failure.
/// </para>
/// <para>
/// Tenant-aware: inherits <c>TenantEntity</c> so per-tenant routing / archival is possible.
/// </para>
/// </remarks>
public class OutboxMessage : TenantEntity<Guid>
{
    /// <summary>
    /// Type marker the consumer uses to route the event (e.g. <c>"Orders.Placed.v1"</c>).
    /// Versioning is the consumer's responsibility — the framework treats this as an opaque
    /// string.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Serialized payload. <c>System.Text.Json</c> is the default convention; a consumer is
    /// free to store another format as long as <c>IOutboxPublisher</c> knows how to read it.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Correlation / trace id propagated from the originating request, so downstream
    /// consumers can stitch events back to the user action that produced them.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// UTC timestamp at which the originating business event happened. Used for both
    /// dispatch ordering and operator forensics.
    /// </summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>
    /// <c>null</c> while pending; stamped to UTC <c>now</c> on successful publish.
    /// </summary>
    public DateTime? ProcessedAtUtc { get; set; }

    /// <summary>
    /// Number of dispatch attempts so far. Bumped by the worker on each publisher failure.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Earliest UTC time at which the worker may retry this row. <c>null</c> = retry
    /// immediately. Set by the worker after a failure using exponential backoff.
    /// </summary>
    public DateTime? NextAttemptUtc { get; set; }

    /// <summary>
    /// Last error message captured from the publisher. <c>null</c> after a successful
    /// publish (cleared by <c>MarkProcessedAsync</c>).
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// <c>true</c> when the row has exceeded <c>OutboxOptions.MaxAttempts</c>. Poisoned rows
    /// are skipped by the polling worker; operators must reset
    /// <c>IsPoisoned = false</c>, <c>AttemptCount = 0</c>, <c>NextAttemptUtc = null</c>
    /// after diagnosing the root cause.
    /// </summary>
    public bool IsPoisoned { get; set; }
}
