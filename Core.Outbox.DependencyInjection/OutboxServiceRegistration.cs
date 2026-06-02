using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NetCoreBackend.NArchitecture.Core.Outbox.Abstractions;
using NetCoreBackend.NArchitecture.Core.Outbox.EfPersistence;
using NetCoreBackend.NArchitecture.Core.Outbox.Worker;

namespace NetCoreBackend.NArchitecture.Core.Outbox.DependencyInjection;

/// <summary>
/// One-call wiring for <c>Core.Outbox</c>: registers the EF-backed store, the background
/// publisher worker, and validates <see cref="OutboxOptions"/> at startup.
/// </summary>
public static class OutboxServiceRegistration
{
    /// <summary>
    /// Register the outbox store (<see cref="EfOutboxStore{TDbContext}"/>) and the
    /// background <see cref="OutboxPublisherWorker"/>.
    /// </summary>
    /// <typeparam name="TDbContext">The consumer <c>DbContext</c> that holds the
    /// <c>OutboxMessage</c> table (configured via
    /// <see cref="EfOutboxModelExtensions.ConfigureOutbox"/>).</typeparam>
    /// <param name="services">The DI container.</param>
    /// <param name="configure">Optional inline configuration callback for
    /// <see cref="OutboxOptions"/>. Bind from <c>appsettings.json</c> separately via
    /// <c>services.Configure&lt;OutboxOptions&gt;(...)</c> if you prefer.</param>
    /// <remarks>
    /// The consumer MUST register a concrete <c>IOutboxPublisher</c> separately — that
    /// piece is integration-specific (RabbitMQ producer, Kafka producer, MediatR
    /// republisher, etc.):
    /// <code>
    /// services.AddOutbox&lt;AppDbContext&gt;(o => { o.MaxAttempts = 5; });
    /// services.AddScoped&lt;IOutboxPublisher, MyRabbitPublisher&gt;();
    /// </code>
    /// For multi-tenant SaaS scenarios, call <c>services.AddMultiTenancy()</c> BEFORE
    /// this method so <c>EfOutboxStore</c> can resolve <c>ITenantEntitySetter</c> for the
    /// per-row <c>TenantId</c> stamp.
    /// </remarks>
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
