# Core.MultiTenancy

SaaS uygulamaları için multi-tenant altyapısı. Tenant tespitini JWT claim, HTTP header ve subdomain üzerinden sırayla yapar.

## Tenant Tespiti (Öncelik Sırası)

```
1. JWT claim: "tenant_id"          → En güvenli, token doğrulandıktan sonra gelir
2. HTTP Header: X-Tenant-ID        → API client'lar ve geliştirme ortamı için
3. Subdomain: acme.yourapp.com     → Production SaaS URL yapısı için
```

## Bileşenler

| Bileşen | Açıklama |
|---|---|
| `Tenant` | Tenant kaydı entity'si (name, identifier/slug, domain, plan, defaultLocale, isActive) |
| `ITenantContext` | Mevcut tenant'ı okumak için DI'a inject edilen interface |
| `TenantContext` | Scoped, request başına sıfırlanan ITenantContext implementasyonu |
| `ITenantService` | Uygulamada implement edilmesi gereken tenant lookup interface'i |
| `TenantEntitySetter` | `ITenantEntitySetter` implementasyonu — Add işlemlerinde TenantId'yi otomatik set eder |
| `TenantMiddleware` | Her request'te tenant'ı çözen middleware. JWT'de tenant_id varken tenant DB'de yoksa 401 döner. |
| `ModelBuilderTenantExtensions` | `builder.ApplyTenantFilters(_tenantContext)` — DbContext.OnModelCreating'de tek satırla EF Core global filter kurar |
| `TenantClaimTypes` | Claim key sabitleri (tenant_id, is_super_admin, is_impersonating) |

## Kurulum

```csharp
// Program.cs
builder.Services.AddMultiTenancy();
// → ITenantContext, TenantContext ve ITenantEntitySetter otomatik kaydedilir

builder.Services.AddScoped<ITenantService, YourTenantService>();

app.UseAuthentication();
app.UseMultiTenancy();   // UseAuthentication'dan SONRA gelmeli
app.UseAuthorization();
```

> **Middleware sırası neden önemli?**
> `TenantMiddleware`'in 1. öncelik kaynağı JWT'deki `tenant_id` claim'idir. Bu claim ancak
> `UseAuthentication()` çalıştıktan sonra `HttpContext.User` üzerinden okunabilir. Sıralama
> ters olursa `User.Claims` boş kalır, middleware doğrudan header/subdomain fallback'lerine
> düşer ve oturum açmış kullanıcılar bile yanlış tenant'a (veya hiçbir tenant'a) yönlendirilir.

`AddMultiTenancy()` şunları kaydeder:
- `TenantContext` (scoped)
- `ITenantContext` → `TenantContext` (scoped)
- `ITenantEntitySetter` → `TenantEntitySetter` (scoped) — ayrıca kaydetmeye gerek yoktur

## Tenant Entity

```csharp
public class Tenant : Entity<Guid>
{
    public string Name { get; set; }
    public string Identifier { get; set; }   // slug: "acme"
    public string? Domain { get; set; }
    public bool IsActive { get; set; }
    public TenantPlanType PlanType { get; set; }
    public string? DefaultLocale { get; set; }  // "tr", "de" — Accept-Language yoksa fallback
}
```

`DefaultLocale`: Client `Accept-Language` header'ı göndermediğinde `LocalizationMiddleware` bu değeri fallback olarak kullanır.

> **`Identifier` unique olmalı.** Framework code-level uniqueness check yapmıyor; DB constraint'i
> consuming app'in `DbContext` konfigürasyonunda eklenmeli:
> ```csharp
> modelBuilder.Entity<Tenant>().HasIndex(t => t.Identifier).IsUnique();
> // Domain için de aynı (Domain nullable, multi-tenant)
> modelBuilder.Entity<Tenant>()
>     .HasIndex(t => t.Domain)
>     .IsUnique()
>     .HasFilter("[Domain] IS NOT NULL");
> ```
> Bu constraint olmazsa `acme` slug'lı iki Tenant kaydı oluşturulabilir, `GetBySlugAsync`
> belirsiz sonuç döndürür.

## SuperAdmin

JWT'de `is_super_admin: true` ve `tenant_id: null` olan kullanıcı tüm tenant verilerine erişir (EF Core global filter bypass edilir). Belirli bir tenant'a geçmek için impersonation token kullanılır.

Detaylı dokümantasyon: [TENANT.md](../TENANT.md)
