using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Abstraction;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.DependencyInjection;

public static class ServiceCollectionLoggingExtensions
{
    // Singleton is intentional: Serilog's ILogger is thread-safe and per-request context
    // (tenant_id, correlation_id, etc.) is propagated through Serilog.Context.LogContext,
    // which uses AsyncLocal under the hood — not through DI scope. Registering as Scoped
    // would force a new logger per request without solving anything and would break sinks
    // that batch.
    public static IServiceCollection AddLogging(this IServiceCollection services, ILogger logger)
    {
        services.AddSingleton(logger);

        return services;
    }
}
