# Core.Localization.Resource.Yaml.DependencyInjection

Extension method that registers YAML localization into the DI container.

## Setup

```csharp
// Program.cs
builder.Services.AddYamlResourceLocalization();
```

`ServiceCollectionResourceLocalizationManagerExtension` scans files matching the `Features/*/Resources/Locales/*.{culture}.yaml` pattern and registers `ResourceLocalizationManager` as `ILocalizationService` with scoped lifetime.

## Usage with Middleware

```csharp
// Middleware for locale detection (Core.Localization.WebApi)
app.UseResponseLocalization();

// Language ordering: automatically taken from the Accept-Language header
// Example: Accept-Language: tr, en;q=0.9
```
