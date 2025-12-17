using Microsoft.AspNetCore.Builder;

namespace NetCoreBackend.NArchitecture.Core.Localization.WebApi;

public static class ApplicationBuilderLocalizationMiddlewareExtensions
{
    public static IApplicationBuilder UseResponseLocalization(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<LocalizationMiddleware>();
    }
}
