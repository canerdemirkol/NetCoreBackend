using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Localization.Abstraction;

namespace NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml.DependencyInjection;

public static class ServiceCollectionResourceLocalizationManagerExtension
{
    /// <summary>
    /// Adds <see cref="ResourceLocalizationManager"/> as <see cref="ILocalizationService"/> to <see cref="IServiceCollection"/>.
    /// <list type="bullet">
    ///    <item>
    ///        <description>Reads all yaml files in the "<see cref="Assembly.GetExecutingAssembly()"/>/Features/{featureName}/Resources/Locales/". Yaml file names must be like {uniqueKeySectionName}.{culture}.yaml.</description>
    ///    </item>
    ///    <item>
    ///        <description>If you don't want separate locale files with sections, create "<see cref="Assembly.GetExecutingAssembly()"/>/Features/Index/Resources/Locales/index.{culture}.yaml".</description>
    ///    </item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddYamlResourceLocalization(this IServiceCollection services)
    {
        services.AddScoped<ILocalizationService, ResourceLocalizationManager>(_ =>
        {
            // <locale, <featureName, resourceDir>>
            Dictionary<string, Dictionary<string, string>> resources = [];

            string[] featureDirs = Directory.GetDirectories(
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Features")
            );
            foreach (string featureDir in featureDirs)
            {
                string featureName = Path.GetFileName(featureDir);
                string localeDir = Path.Combine(featureDir, "Resources", "Locales");
                if (Directory.Exists(localeDir))
                {
                    string[] localeFiles = Directory.GetFiles(localeDir);
                    foreach (string localeFile in localeFiles)
                    {
                        // Expected pattern: "{section}.{culture}.yaml" — reject files that don't
                        // follow it instead of silently using the whole file name as the culture.
                        string localeName = Path.GetFileNameWithoutExtension(localeFile);
                        int separatorIndex = localeName.IndexOf('.');
                        if (separatorIndex < 0 || separatorIndex == localeName.Length - 1)
                            throw new InvalidOperationException(
                                $"Locale file '{localeFile}' does not match the required '{{section}}.{{culture}}.yaml' naming pattern.");

                        string localeCulture = localeName[(separatorIndex + 1)..];

                        if (File.Exists(localeFile))
                        {
                            if (!resources.ContainsKey(localeCulture))
                                resources.Add(localeCulture, []);
                            resources[localeCulture].Add(featureName, localeFile);
                        }
                    }
                }
            }

            return new ResourceLocalizationManager(resources);
        });

        return services;
    }
}
