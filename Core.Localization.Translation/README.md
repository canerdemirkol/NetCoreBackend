# Core.Localization.Translation

Localization implementation that performs real-time translation via `ITranslationService`.

## How It Works

`TranslateLocalizationManager` implements `ILocalizationService` and translates the requested key into the target language using `ITranslationService.TranslateAsync()`.

```
GetLocalizedAsync("UserNotFound", locales: ["tr"]) 
  → ITranslationService.TranslateAsync("UserNotFound", targetLanguage: "tr")
  → AWS Translate / Custom servis
  → "Kullanıcı bulunamadı."
```

## Difference: YAML vs Translation

| | YAML Localization | Translation Localization |
|---|---|---|
| Source | Static `.yaml` files | Dynamic translation service |
| Latency | Zero (cached) | Network round-trip |
| Language support | Only defined locales | Any language |
| Cost | None | API cost |

## DI Registration

```csharp
// With Amazon Translate
builder.Services.AddAmazonTranslate(config);
builder.Services.AddScoped<ILocalizationService, TranslateLocalizationManager>();
```
