# Core.Translation.AmazonTranslate

`ITranslationService`'in AWS Translate tabanlı implementasyonu.

## Kurulum

```csharp
// Program.cs
builder.Services.AddAmazonTranslate(new AmazonTranslateConfiguration
{
    AccessKey = "your-access-key",
    SecretKey = "your-secret-key",
    RegionEndpoint = "eu-west-1"
});
```

Ya da appsettings.json:

```json
{
  "AmazonTranslateConfiguration": {
    "AccessKey": "AKIAIOSFODNN7EXAMPLE",
    "SecretKey": "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
    "RegionEndpoint": "eu-west-1"
  }
}
```

## Desteklenen Diller

AWS Translate 75+ dili destekler. Tam liste: [AWS Translate Supported Languages](https://docs.aws.amazon.com/translate/latest/dg/what-is-languages.html)

## Kullanım Senaryosu

Çoğunlukla [`Core.Localization.Translation`](../Core.Localization.Translation/README.md) üzerinden dolaylı kullanılır:

```
ILocalizationService
  → TranslateLocalizationManager
  → ITranslationService
  → AmazonTranslateLocalizationManager
  → AWS Translate API
```
