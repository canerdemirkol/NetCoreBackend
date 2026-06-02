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
        services.AddOptions<OutboxOptions>();
        if (configure is not null)
            services.Configure(configure);

        services.TryAddScoped<IOutboxStore, EfOutboxStore<TDbContext>>();
        services.AddHostedService<OutboxPublisherWorker>();
        return services;
    }
}
