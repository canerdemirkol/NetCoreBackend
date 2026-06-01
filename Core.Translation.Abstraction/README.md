# Core.Translation.Abstraction

Çeviri servisi arayüzü.

## Interface

```csharp
public interface ITranslationService
{
    Task<string> TranslateAsync(string text, string to, string from = "en");
}
```

## Kullanım

```csharp
string translated = await translationService.TranslateAsync(
    text: "Hello World",
    to: "tr",
    from: "en"   // varsayılan "en", belirtilmezse İngilizce kaynak kabul edilir
);
// → "Merhaba Dünya"
```

`to` ve `from` parametreleri için BCP 47 dil kodları kullanılır (örn. `tr`, `en`, `de`, `fr`).

## Implementasyon

[`Core.Translation.AmazonTranslate`](../Core.Translation.AmazonTranslate/README.md) — AWS Translate entegrasyonu
