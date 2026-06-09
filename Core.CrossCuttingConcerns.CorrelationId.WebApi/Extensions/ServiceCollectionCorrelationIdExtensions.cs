using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.Abstractions;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.WebApi.Accessors;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.WebApi.Extensions;

public static class ServiceCollectionCorrelationIdExtensions
{
    public static IServiceCollection AddCorrelationId(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();
        return services;
    }
}
