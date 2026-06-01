# Core.Localization.WebApi

HTTP `Accept-Language` header'ından locale tespiti yapan middleware.

## Kurulum

```csharp
// Program.cs
app.UseResponseLocalization();
```

## Nasıl Çalışır

`LocalizationMiddleware`, her request'te `Accept-Language` header'ını okur ve `ILocalizationService.AcceptLocales`'i set eder. Header yoksa tenant'ın `DefaultLocale`'ine fallback yapar:

```
// Durum 1: Client dil gönderiyor
GET /api/products
Accept-Language: tr, en;q=0.9, de;q=0.8
→ AcceptLocales = ["tr", "en", "de"]

// Durum 2: Client dil göndermiyor, tenant'ın defaultLocale'i "de"
GET /api/products
→ AcceptLocales = ["de"]   ← Tenant.DefaultLocale'den geldi

// Durum 3: Ne header ne DefaultLocale var
→ AcceptLocales = null  ← lokalizasyon servisi kendi default'una (en) düşer
```

## Middleware Sırası

`LocalizationMiddleware`, `ITenantContext.DefaultLocale`'i okuduğu için `UseMultiTenancy()`'den **sonra** gelmeli:

```csharp
app.UseRouting();
app.UseAuthentication();
app.UseMultiTenancy();        // TenantContext.DefaultLocale set edilir
app.UseResponseLocalization(); // DefaultLocale'i okur
app.UseAuthorization();
app.MapControllers();
```

Locale bilgisi request boyunca `ILocalizationService` üzerinden erişilebilir olur.
