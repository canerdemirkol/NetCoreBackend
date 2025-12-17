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
        services.AddTransient<ITranslationService, AmazonTranslateLocalizationManager>(
            _ => new AmazonTranslateLocalizationManager(configuration)
        );
        return services;
    }
}
