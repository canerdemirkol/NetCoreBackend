using System.Collections.Immutable;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using NetCoreBackend.NArchitecture.Core.Localization.Abstraction;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;

namespace NetCoreBackend.NArchitecture.Core.Localization.WebApi;

/// <summary>
/// Picks the request's locales: the Accept-Language header, else the tenant's default locale, else
/// <see cref="LocalizationMiddlewareOptions.DefaultLocale"/>. The first one also becomes the request's UI culture, so
/// culture-aware messages (e.g. validation) follow it; the formatting culture is left alone on purpose.
/// </summary>
public class LocalizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly LocalizationMiddlewareOptions _options;

    public LocalizationMiddleware(RequestDelegate next, LocalizationMiddlewareOptions? options = null)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options ?? new LocalizationMiddlewareOptions();
    }

    public async Task Invoke(HttpContext context, ILocalizationService localizationService, ITenantContext tenantContext)
    {
        IList<StringWithQualityHeaderValue> acceptLanguages = context.Request.GetTypedHeaders().AcceptLanguage;
        if (acceptLanguages.Count > 0)
        {
            // Filter to valid BCP 47 locale codes only. A malformed Accept-Language (random
            // junk from a bot, "xx-YYY" typos, q=0 entries) would otherwise propagate into
            // the localization service and cause repeated lookups that always miss.
            ImmutableArray<string> locales = acceptLanguages
                .OrderByDescending(x => x.Quality ?? 1)
                .Where(x => (x.Quality ?? 1) > 0)
                .Select(x => x.Value.ToString())
                .Where(IsValidLocale)
                .ToImmutableArray();

            if (locales.Length > 0)
                localizationService.AcceptLocales = locales;
            else if (tenantContext.DefaultLocale is not null)
                localizationService.AcceptLocales = [tenantContext.DefaultLocale];
        }
        else if (tenantContext.DefaultLocale is not null)
            localizationService.AcceptLocales = [tenantContext.DefaultLocale];

        if (localizationService.AcceptLocales is null && IsValidLocale(_options.DefaultLocale))
            localizationService.AcceptLocales = [_options.DefaultLocale!];

        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        string? chosen = localizationService.AcceptLocales?.FirstOrDefault(IsValidLocale);
        if (chosen is not null)
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(chosen);
        try
        {
            await _next(context);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private static bool IsValidLocale(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code == "*") return false;
        try
        {
            CultureInfo.GetCultureInfo(code);
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
