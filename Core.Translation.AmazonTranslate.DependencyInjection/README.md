# Core.Translation.AmazonTranslate.DependencyInjection

AWS Translate servisini DI container'a kaydeden extension metot.

## Kurulum

```csharp
// Program.cs
var amazonConfig = builder.Configuration
    .GetSection("AmazonTranslateConfiguration")
    .Get<AmazonTranslateConfiguration>()!;

builder.Services.AddAmazonTranslation(amazonConfig);
```

`AddAmazonTranslation`, `AmazonTranslateLocalizationManager`'ı `ITranslationService` olarak **transient** kaydeder.

## Tam Lokalizasyon Entegrasyonu

```csharp
// YAML lokalizasyon yerine AWS Translate ile dinamik çeviri
builder.Services.AddAmazonTranslation(amazonConfig);
builder.Services.AddScoped<ILocalizationService, TranslateLocalizationManager>();

// Middleware
app.UseResponseLocalization();
```
