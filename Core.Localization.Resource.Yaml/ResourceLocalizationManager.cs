using System.Data;
using NetCoreBackend.NArchitecture.Core.Localization.Abstraction;
using YamlDotNet.RepresentationModel;

namespace NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml;

public class ResourceLocalizationManager : ILocalizationService
{
    private const string _defaultLocale = "en";
    private const string _defaultKeySection = "index";
    public ICollection<string>? AcceptLocales { get; set; }

    // <locale, <section, <path, content>>>
    private readonly Dictionary<string, Dictionary<string, (string path, YamlMappingNode? content)>> _resourceData = [];
    // Per-instance lock prevents the same YAML file from being parsed twice when
    // multiple requests miss the cache simultaneously.
    private readonly object _loadLock = new();

    public ResourceLocalizationManager(Dictionary<string, Dictionary<string, string>> resources)
    {
        foreach ((string locale, Dictionary<string, string> sectionResources) in resources)
        {
            if (!_resourceData.ContainsKey(locale))
                _resourceData.Add(locale, new Dictionary<string, (string path, YamlMappingNode? value)>());

            foreach ((string sectionName, string sectionResourcePath) in sectionResources)
                _resourceData[locale].Add(sectionName, (sectionResourcePath, null));
        }
    }

    public Task<string> GetLocalizedAsync(string key, string? keySection = null)
    {
        return GetLocalizedAsync(key, AcceptLocales ?? throw new NoNullAllowedException(nameof(AcceptLocales)), keySection);
    }

    public Task<string> GetLocalizedAsync(string key, ICollection<string> acceptLocales, string? keySection = null)
    {
        string? localization;
        if (acceptLocales is not null)
            foreach (string locale in acceptLocales)
            {
                localization = getLocalizationFromResource(key, locale, keySection);
                if (localization is not null)
                    return Task.FromResult(localization);
            }

        localization = getLocalizationFromResource(key, _defaultLocale, keySection);
        if (localization is not null)
            return Task.FromResult(localization);

        return Task.FromResult(key);
    }

    private string? getLocalizationFromResource(string key, string locale, string? keySection = _defaultKeySection)
    {
        if (string.IsNullOrWhiteSpace(keySection))
            keySection = _defaultKeySection;

        if (
            _resourceData.TryGetValue(locale, out Dictionary<string, (string path, YamlMappingNode? content)>? cultureNode)
            && cultureNode.TryGetValue(keySection, out (string path, YamlMappingNode? content) sectionNode)
        )
        {
            if (sectionNode.content is null)
            {
                lock (_loadLock)
                {
                    // Double-check after acquiring the lock: another thread may have loaded it.
                    sectionNode = cultureNode[keySection];
                    if (sectionNode.content is null)
                    {
                        YamlMappingNode? loaded = lazyLoadResource(sectionNode.path);
                        sectionNode = (sectionNode.path, loaded);
                        cultureNode[keySection] = sectionNode;
                    }
                }
            }

            if (sectionNode.content is not null
                && sectionNode.content.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? cultureValueNode))
                return cultureValueNode.ToString();
        }

        return null;
    }

    // Hard ceiling on the size of a single localization YAML file. Resource files in this
    // project are checked-in translation tables — a real one is measured in kilobytes, not
    // megabytes. The cap is a defense-in-depth limit so a malicious or accidentally-massive
    // file (zip-bomb-style expansion via aliases, runaway generator) cannot exhaust memory
    // during startup.
    private const long _maxResourceFileSizeBytes = 2 * 1024 * 1024; // 2 MiB

    private static YamlMappingNode? lazyLoadResource(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Localization resource file not found: {path}", path);

        long size = new FileInfo(path).Length;
        if (size > _maxResourceFileSizeBytes)
            throw new InvalidOperationException(
                $"Localization resource file '{path}' is {size} bytes, exceeding the {_maxResourceFileSizeBytes}-byte limit.");

        using StreamReader reader = new(path);
        YamlStream yamlStream = [];
        yamlStream.Load(reader);

        if (yamlStream.Documents.Count == 0)
            return null;   // empty file — caller treats missing content as "key not found"

        if (yamlStream.Documents[0].RootNode is not YamlMappingNode mapping)
            throw new InvalidOperationException(
                $"Localization resource file '{path}' is not a YAML mapping at the root.");

        return mapping;
    }
}
