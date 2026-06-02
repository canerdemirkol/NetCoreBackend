using System.Collections.Immutable;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using NetCoreBackend.NArchitecture.Core.Localization.Abstraction;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;

namespace NetCoreBackend.NArchitecture.Core.Localization.WebApi;

public class LocalizationMiddleware
{
    private readonly RequestDelegate _next;

    public LocalizationMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
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

        await _next(context);
    }

    private static bool IsValidLocale(string code)
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
