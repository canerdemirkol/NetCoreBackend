using NetCoreBackend.NArchitecture.Core.Outbox.Entities;

namespace NetCoreBackend.NArchitecture.Core.Outbox.Abstractions;

// Consumer-supplied: knows how to actually ship an outbox row to the downstream system
// (RabbitMQ, Kafka, SNS, internal MediatR notifications — whatever the app uses).
//
// Throwing here triggers retry. Returning normally is treated as "successfully delivered"
// and the row is marked ProcessedAtUtc by the worker.
public interface IOutboxPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
