# Core.Localization.WebApi

Middleware that detects the locale from the HTTP `Accept-Language` header.

## Setup

```csharp
// Program.cs
app.UseResponseLocalization();
```

## How It Works

On every request, `LocalizationMiddleware` reads the `Accept-Language` header and sets `ILocalizationService.AcceptLocales`. If the header is absent, it falls back to the tenant's `DefaultLocale`:

```
// Case 1: Client sends languages
GET /api/products
Accept-Language: tr, en;q=0.9, de;q=0.8
→ AcceptLocales = ["tr", "en", "de"]

// Case 2: Client sends no language, tenant's defaultLocale is "de"
GET /api/products
→ AcceptLocales = ["de"]   ← Came from Tenant.DefaultLocale

// Case 3: Neither header nor DefaultLocale is present
→ AcceptLocales = null  ← localization service falls back to its own default (en)
```

## Middleware Order

Because `LocalizationMiddleware` reads `ITenantContext.DefaultLocale`, it must come **after** `UseMultiTenancy()`:

```csharp
app.UseRouting();
app.UseAuthentication();
app.UseMultiTenancy();        // TenantContext.DefaultLocale is set
app.UseResponseLocalization(); // Reads DefaultLocale
app.UseAuthorization();
app.MapControllers();
```

The locale information becomes accessible throughout the request via `ILocalizationService`.
