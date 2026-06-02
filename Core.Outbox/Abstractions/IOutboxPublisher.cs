using NetCoreBackend.NArchitecture.Core.Outbox.Entities;

namespace NetCoreBackend.NArchitecture.Core.Outbox.Abstractions;

/// <summary>
/// Consumer-supplied: ships an outbox row to the downstream system
/// (RabbitMQ, Kafka, SNS, internal MediatR notifications — whatever the app uses).
/// </summary>
/// <remarks>
/// Throwing here triggers retry by the worker. Returning normally is treated as
/// "successfully delivered" and the row is marked <c>ProcessedAtUtc</c>.
/// </remarks>
public interface IOutboxPublisher
{
    /// <summary>
    /// Ship a single outbox message to the downstream broker.
    /// </summary>
    /// <param name="message">The outbox row to publish. <c>message.TenantId</c> is preserved
    /// and may be used for per-tenant routing decisions.</param>
    /// <param name="cancellationToken">Cancelled on host shutdown.
    /// <see cref="OperationCanceledException"/> is treated by the worker as "don't penalise
    /// this row" — it will be re-fetched on the next run, not marked as failed.</param>
    /// <exception cref="Exception">Any exception (other than cancellation) is recorded as
    /// a publish failure: the worker bumps <c>AttemptCount</c>, schedules
    /// <c>NextAttemptUtc</c> with exponential backoff, or marks the row poisoned after
    /// <c>OutboxOptions.MaxAttempts</c>.</exception>
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
