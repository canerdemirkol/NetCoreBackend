# Core.Translation.Abstraction

Çeviri servisi arayüzü.

## Interface

```csharp
public interface ITranslationService
{
    Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = null);
}
```

## Kullanım

```csharp
string translated = await translationService.TranslateAsync(
    text: "Hello World",
    targetLanguage: "tr",
    sourceLanguage: "en"   // null bırakılırsa otomatik tespit
);
// → "Merhaba Dünya"
```

`targetLanguage` ve `sourceLanguage` için BCP 47 dil kodları kullanılır (örn. `tr`, `en`, `de`, `fr`).

## Implementasyon

[`Core.Translation.AmazonTranslate`](../Core.Translation.AmazonTranslate/README.md) — AWS Translate entegrasyonu
