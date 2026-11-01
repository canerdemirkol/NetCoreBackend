using Microsoft.AspNetCore.Builder;

namespace NetCoreBackend.NArchitecture.Core.Localization.WebApi;

public static class ApplicationBuilderLocalizationMiddlewareExtensions
{
    public static IApplicationBuilder UseResponseLocalization(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<LocalizationMiddleware>();
    }

    /// <summary>Like <see cref="UseResponseLocalization(IApplicationBuilder)"/>, with a locale for requests that name none.</summary>
    public static IApplicationBuilder UseResponseLocalization(this IApplicationBuilder builder, string defaultLocale)
    {
        return builder.UseMiddleware<LocalizationMiddleware>(new LocalizationMiddlewareOptions { DefaultLocale = defaultLocale });
    }
}
