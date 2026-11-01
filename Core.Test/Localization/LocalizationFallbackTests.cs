using System.Globalization;
using Microsoft.AspNetCore.Http;
using Moq;
using NetCoreBackend.NArchitecture.Core.Localization.Abstraction;
using NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml;
using NetCoreBackend.NArchitecture.Core.Localization.Translation;
using NetCoreBackend.NArchitecture.Core.Localization.WebApi;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;
using NetCoreBackend.NArchitecture.Core.Translation.Abstraction;

namespace NetCoreBackend.NArchitecture.Core.Test.Localization;

/// <summary>
/// A request with no usable language (no Accept-Language, no tenant locale) must still get localized messages in the
/// configured default language instead of an exception (which surfaced as HTTP 500), and the chosen language must
/// reach the request's UI culture, so validation messages follow it.
/// </summary>
public class LocalizationFallbackTests : IDisposable
{
    private const string Section = "auth";
    private const string Key = "InvalidCredentials";
    private readonly string _directory = Directory.CreateTempSubdirectory("core-localization-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private ResourceLocalizationManager YamlManager(string? defaultLocale = null)
    {
        string tr = Path.Combine(_directory, "auth.tr.yaml");
        string en = Path.Combine(_directory, "auth.en.yaml");
        File.WriteAllText(tr, $"{Key}: \"E-posta veya şifre hatalı.\"\n");
        File.WriteAllText(en, $"{Key}: \"Wrong e-mail or password.\"\n");
        var resources = new Dictionary<string, Dictionary<string, string>>
        {
            ["tr"] = new() { [Section] = tr },
            ["en"] = new() { [Section] = en },
        };
        return defaultLocale is null ? new ResourceLocalizationManager(resources) : new ResourceLocalizationManager(resources, defaultLocale);
    }

    [Fact]
    public async Task TheYamlManager_WithoutAcceptLocales_UsesItsDefaultLocale_InsteadOfThrowing()
    {
        ResourceLocalizationManager manager = YamlManager(defaultLocale: "tr");

        string message = await manager.GetLocalizedAsync(Key, Section);

        Assert.Equal("E-posta veya şifre hatalı.", message);
    }

    [Fact]
    public async Task TheYamlManager_KeepsEnglishAsItsDefault_WhenNoneIsGiven()
    {
        ResourceLocalizationManager manager = YamlManager();

        Assert.Equal("Wrong e-mail or password.", await manager.GetLocalizedAsync(Key, Section));
    }

    [Fact]
    public async Task TheTranslationManager_WithoutAcceptLocales_TranslatesToItsDefaultLocale()
    {
        var translations = new Mock<ITranslationService>();
        translations.Setup(t => t.TranslateAsync(Key, "tr", It.IsAny<string>())).ReturnsAsync("Çeviri");
        var manager = new TranslateLocalizationManager(translations.Object, defaultLocale: "tr");

        Assert.Equal("Çeviri", await manager.GetLocalizedAsync(Key));
    }

    private static async Task<(ICollection<string>? Locales, CultureInfo UiCulture)> RunMiddlewareAsync(
        string? acceptLanguage, string? tenantLocale, LocalizationMiddlewareOptions? options)
    {
        var localization = new Mock<ILocalizationService>();
        localization.SetupProperty(l => l.AcceptLocales);
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.DefaultLocale).Returns(tenantLocale);
        var context = new DefaultHttpContext();
        if (acceptLanguage is not null)
            context.Request.Headers.AcceptLanguage = acceptLanguage;

        // A culture none of the cases ask for, so a middleware that leaves the culture alone is caught.
        CultureInfo before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        CultureInfo seen = CultureInfo.InvariantCulture;
        try
        {
            var middleware = new LocalizationMiddleware(_ =>
            {
                seen = CultureInfo.CurrentUICulture;
                return Task.CompletedTask;
            }, options);
            await middleware.Invoke(context, localization.Object, tenant.Object);
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
        return (localization.Object.AcceptLocales, seen);
    }

    [Fact]
    public async Task WithoutAHeaderOrATenantLocale_TheMiddleware_UsesTheConfiguredDefault()
    {
        (ICollection<string>? locales, CultureInfo uiCulture) = await RunMiddlewareAsync(null, null, new LocalizationMiddlewareOptions { DefaultLocale = "tr" });

        Assert.Equal(["tr"], locales);
        Assert.Equal("tr", uiCulture.Name);
    }

    [Fact]
    public async Task TheHeader_StillComesFirst_AndSetsTheUiCulture()
    {
        (ICollection<string>? locales, CultureInfo uiCulture) = await RunMiddlewareAsync("en-US,en;q=0.8", "tr", new LocalizationMiddlewareOptions { DefaultLocale = "tr" });

        Assert.Equal("en-US", locales!.First());
        Assert.Equal("en-US", uiCulture.Name);
    }

    [Fact]
    public async Task TheTenantLocale_ComesBeforeTheConfiguredDefault()
    {
        (ICollection<string>? locales, _) = await RunMiddlewareAsync(null, "en", new LocalizationMiddlewareOptions { DefaultLocale = "tr" });

        Assert.Equal(["en"], locales);
    }

    [Fact]
    public async Task WithoutOptions_TheMiddleware_BehavesAsBefore()
    {
        (ICollection<string>? locales, _) = await RunMiddlewareAsync(null, null, options: null);

        Assert.Null(locales);
    }
}
