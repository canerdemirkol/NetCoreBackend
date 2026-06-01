# Core.Localization.Abstraction

Lokalizasyon servisi arayüzü. Tüm lokalizasyon implementasyonlarının uyduğu contract.

## Interface

```csharp
public interface ILocalizationService
{
    IList<string> AcceptLocales { get; set; }  // Request'ten gelen dil listesi

    Task<string> GetLocalizedAsync(string key);
    Task<string> GetLocalizedAsync(string key, IList<string> locales);
    Task<string> GetLocalizedAsync(string key, string section);
    Task<string> GetLocalizedAsync(string key, IList<string> locales, string section);
}
```

## Kullanım

```csharp
public class MyCommandHandler
{
    private readonly ILocalizationService _localization;

    public async Task Handle(...)
    {
        string message = await _localization.GetLocalizedAsync("UserNotFound", "Users");
        throw new NotFoundException(message);
    }
}
```

## Implementasyonlar

| Proje | Açıklama |
|---|---|
| [`Core.Localization.Resource.Yaml`](../Core.Localization.Resource.Yaml/README.md) | YAML dosyalarından statik çeviri |
| [`Core.Localization.Translation`](../Core.Localization.Translation/README.md) | `ITranslationService` üzerinden dinamik çeviri |
