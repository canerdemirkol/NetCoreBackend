using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Translation.Abstraction;
using NetCoreBackend.NArchitecture.Core.Translation.AmazonTranslate;

namespace NetCoreBackend.NArchitecture.Core.Translation.AmazonTranslate.DependencyInjection;

public static class ServiceCollectionAmazonTranslateLocalizationExtension
{
    public static IServiceCollection AddAmazonTranslation(
        this IServiceCollection services,
        AmazonTranslateConfiguration configuration
    )
    {
        // Singleton: AmazonTranslateClient is thread-safe and maintains an internal HTTP
        // connection pool, retry policy, and credential cache. Registering as Transient
        // recreated the client (and its TLS handshake / IMDS credential refresh) on every
        // request, which dominates the latency of a translation call.
        services.AddSingleton<ITranslationService, AmazonTranslateLocalizationManager>(
            _ => new AmazonTranslateLocalizationManager(configuration)
        );
        return services;
    }
}
