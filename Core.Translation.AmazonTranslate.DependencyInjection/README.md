# Core.Translation.AmazonTranslate.DependencyInjection

Extension method that registers the AWS Translate service in the DI container.

## Installation

```csharp
// Program.cs
var amazonConfig = builder.Configuration
    .GetSection("AmazonTranslateConfiguration")
    .Get<AmazonTranslateConfiguration>()!;

builder.Services.AddAmazonTranslation(amazonConfig);
```

`AddAmazonTranslation` registers `AmazonTranslateLocalizationManager` as `ITranslationService` with a **transient** lifetime.

## Full Localization Integration

```csharp
// Dynamic translation with AWS Translate instead of YAML localization
builder.Services.AddAmazonTranslation(amazonConfig);
builder.Services.AddScoped<ILocalizationService, TranslateLocalizationManager>();

// Middleware
app.UseResponseLocalization();
```
