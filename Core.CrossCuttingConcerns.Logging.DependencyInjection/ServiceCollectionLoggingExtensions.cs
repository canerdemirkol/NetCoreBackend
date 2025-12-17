using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Abstraction;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.DependencyInjection;

public static class ServiceCollectionLoggingExtensions
{
    public static IServiceCollection AddLogging(this IServiceCollection services, ILogger logger)
    {
        services.AddSingleton(logger);

        return services;
    }
}
