using NetCoreBackend.NArchitecture.Core.Localization.Abstraction;
using NetCoreBackend.NArchitecture.Core.Translation.Abstraction;

namespace NetCoreBackend.NArchitecture.Core.Localization.Translation;

public class TranslateLocalizationManager : ILocalizationService
{
    public const string EnglishLocale = "en";
    private readonly string _defaultLocale;
    public ICollection<string>? AcceptLocales { get; set; }

    private readonly ITranslationService _translationService;

    public TranslateLocalizationManager(ITranslationService translationService) : this(translationService, EnglishLocale) { }

    public TranslateLocalizationManager(ITranslationService translationService, string defaultLocale)
    {
        _defaultLocale = defaultLocale;
        _translationService = translationService;
    }

    public Task<string> GetLocalizedAsync(string key, string? keySection = null)
    {
        return GetLocalizedAsync(key, AcceptLocales ?? []);
    }

    public async Task<string> GetLocalizedAsync(string key, ICollection<string> acceptLocales, string? keySection = null)
    {
        string? localization;

        if (acceptLocales is not null)
            foreach (string locale in acceptLocales)
            {
                localization = await _translationService.TranslateAsync(key, locale);
                if (!string.IsNullOrWhiteSpace(localization))
                    return localization;
            }

        localization = await _translationService.TranslateAsync(key, _defaultLocale);
        if (!string.IsNullOrWhiteSpace(localization))
            return localization;

        return key;
    }
}
