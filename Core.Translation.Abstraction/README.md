# Core.Translation.Abstraction

Translation service interface.

## Interface

```csharp
public interface ITranslationService
{
    Task<string> TranslateAsync(string text, string to, string from = "en");
}
```

## Usage

```csharp
string translated = await translationService.TranslateAsync(
    text: "Hello World",
    to: "tr",
    from: "en"   // defaults to "en"; if not specified, an English source is assumed
);
// → "Merhaba Dünya"
```

The `to` and `from` parameters use BCP 47 language codes (e.g. `tr`, `en`, `de`, `fr`).

## Implementation

[`Core.Translation.AmazonTranslate`](../Core.Translation.AmazonTranslate/README.md) — AWS Translate integration
