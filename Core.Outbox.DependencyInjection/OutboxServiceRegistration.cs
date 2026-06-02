using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NetCoreBackend.NArchitecture.Core.Outbox.Abstractions;
using NetCoreBackend.NArchitecture.Core.Outbox.EfPersistence;
using NetCoreBackend.NArchitecture.Core.Outbox.Worker;

namespace NetCoreBackend.NArchitecture.Core.Outbox.DependencyInjection;

public static class OutboxServiceRegistration
{
    // Register outbox storage + the background publisher. Consumer is required to register
    // IOutboxPublisher themselves — that's the integration-specific piece (RabbitMQ producer,
    // Kafka producer, MediatR republisher, etc.).
    //
    // Usage:
    //   services.AddOutbox<AppDbContext>(o => { o.MaxAttempts = 5; });
    //   services.AddScoped<IOutboxPublisher, MyRabbitPublisher>();
    public static IServiceCollection AddOutbox<TDbContext>(
        this IServiceCollection services,
        Action<OutboxOptions>? configure = null) where TDbContext : DbContext
    {
        // Bind + validate at startup so misconfigured options (BatchSize=0,
        // MaxRetryDelay < BaseRetryDelay, etc.) surface during host build instead of
        // silently degrading worker behavior at runtime.
        var optionsBuilder = services.AddOptions<OutboxOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);
        optionsBuilder
            .Validate(opt => { opt.Validate(); return true; }, "OutboxOptions validation failed")
            .ValidateOnStart();

        services.TryAddScoped<IOutboxStore, EfOutboxStore<TDbContext>>();
        services.AddHostedService<OutboxPublisherWorker>();
        return services;
    }
}
