# Core.Localization.Abstraction

Localization service interface. The contract that all localization implementations adhere to.

## Interface

```csharp
public interface ILocalizationService
{
    ICollection<string>? AcceptLocales { get; set; }  // List of languages received from the request

    Task<string> GetLocalizedAsync(string key, string? keySection = null);
    Task<string> GetLocalizedAsync(string key, ICollection<string> acceptLocales, string? keySection = null);
}
```

## Usage

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

## Implementations

| Project | Description |
|---|---|
| [`Core.Localization.Resource.Yaml`](../Core.Localization.Resource.Yaml/README.md) | Static translation from YAML files |
| [`Core.Localization.Translation`](../Core.Localization.Translation/README.md) | Dynamic translation via `ITranslationService` |
