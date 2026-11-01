namespace NetCoreBackend.NArchitecture.Core.Localization.WebApi;

public class LocalizationMiddlewareOptions
{
    /// <summary>
    /// The locale for a request with no usable Accept-Language and no tenant locale (e.g. "tr"). Null keeps the
    /// earlier behaviour: no locale is set and the localization service falls back to its own default.
    /// </summary>
    public string? DefaultLocale { get; set; }
}
