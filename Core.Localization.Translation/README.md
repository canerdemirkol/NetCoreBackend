# Core.Localization.Translation

`ITranslationService` üzerinden gerçek zamanlı çeviri yapan lokalizasyon implementasyonu.

## Nasıl Çalışır

`TranslateLocalizationManager`, `ILocalizationService`'i implement eder ve istenen key'i `ITranslationService.TranslateAsync()` ile hedef dile çevirir.

```
GetLocalizedAsync("UserNotFound", locales: ["tr"]) 
  → ITranslationService.TranslateAsync("UserNotFound", targetLanguage: "tr")
  → AWS Translate / Custom servis
  → "Kullanıcı bulunamadı."
```

## Fark: YAML vs Translation

| | YAML Lokalizasyon | Translation Lokalizasyon |
|---|---|---|
| Kaynak | Statik `.yaml` dosyaları | Dinamik çeviri servisi |
| Gecikme | Sıfır (cache'li) | Network round-trip |
| Dil desteği | Sadece tanımlı locale'ler | Herhangi bir dil |
| Maliyet | Yok | API maliyeti |

## DI Kaydı

```csharp
// Amazon Translate ile
builder.Services.AddAmazonTranslate(config);
builder.Services.AddScoped<ILocalizationService, TranslateLocalizationManager>();
```
