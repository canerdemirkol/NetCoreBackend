# Core.Translation.AmazonTranslate

AWS Translate-based implementation of `ITranslationService`.

## Installation

```csharp
// Program.cs
builder.Services.AddAmazonTranslate(new AmazonTranslateConfiguration
{
    AccessKey = "your-access-key",
    SecretKey = "your-secret-key",
    RegionEndpoint = "eu-west-1"
});
```

Or via appsettings.json:

```json
{
  "AmazonTranslateConfiguration": {
    "AccessKey": "AKIAIOSFODNN7EXAMPLE",
    "SecretKey": "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
    "RegionEndpoint": "eu-west-1"
  }
}
```

## Supported Languages

AWS Translate supports 75+ languages. Full list: [AWS Translate Supported Languages](https://docs.aws.amazon.com/translate/latest/dg/what-is-languages.html)

## Usage Scenario

It is mostly used indirectly through [`Core.Localization.Translation`](../Core.Localization.Translation/README.md):

```
ILocalizationService
  → TranslateLocalizationManager
  → ITranslationService
  → AmazonTranslateLocalizationManager
  → AWS Translate API
```
