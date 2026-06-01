using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Application.Pipelines.Authorization;
using NetCoreBackend.NArchitecture.Core.Application.Pipelines.Caching;
using NetCoreBackend.NArchitecture.Core.Application.Pipelines.Logging;
using NetCoreBackend.NArchitecture.Core.Application.Pipelines.Performance;
using NetCoreBackend.NArchitecture.Core.Application.Pipelines.Tenancy;
using NetCoreBackend.NArchitecture.Core.Application.Pipelines.Transaction;
using NetCoreBackend.NArchitecture.Core.Application.Pipelines.Validation;

namespace NetCoreBackend.NArchitecture.Core.Application.DependencyInjection;

/// <summary>
/// One-call wiring for every MediatR pipeline behavior shipped with the framework.
/// Each behavior is opt-in per request via its marker interface (e.g. <see cref="ISecuredRequest"/>),
/// so registering all of them costs nothing for requests that don't opt in.
///
/// Consuming apps that need to cherry-pick should register the behaviors individually instead.
/// </summary>
public static class PipelineBehaviorRegistration
{
    public static IServiceCollection AddNArchitecturePipelineBehaviors(this IServiceCollection services)
    {
        // Order matters: registrations resolved in the order they appear here, so guard-style
        // behaviors (auth, validation) run before side-effecting ones (caching, transactions).
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(SuperAdminBlockBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TenantValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CacheRemovingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionScopeBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        return services;
    }
}
