# Core.Localization.Resource.Yaml.DependencyInjection

YAML lokalizasyonunu DI container'a kaydeden extension metot.

## Kurulum

```csharp
// Program.cs
builder.Services.AddYamlLocalization();
```

`ServiceCollectionResourceLocalizationManagerExtension`, `Features/*/Resources/Locales/*.{culture}.yaml` pattern'indeki dosyaları tarar ve `ResourceLocalizationManager`'ı `ILocalizationService` olarak scoped kaydeder.

## Middleware ile Birlikte Kullanım

```csharp
// Locale tespiti için middleware (Core.Localization.WebApi)
app.UseLocalizationMiddleware();

// Dil sıralaması: Accept-Language header'dan otomatik alınır
// Örnek: Accept-Language: tr, en;q=0.9
```
