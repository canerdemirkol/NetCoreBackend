using System.Collections.Immutable;
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
            localizationService.AcceptLocales = acceptLanguages
                .OrderByDescending(x => x.Quality ?? 1)
                .Select(x => x.Value.ToString())
                .ToImmutableArray();
        else if (tenantContext.DefaultLocale is not null)
            localizationService.AcceptLocales = [tenantContext.DefaultLocale];

        await _next(context);
    }
}
