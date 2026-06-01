# Core.Application

CQRS ve MediatR tabanlı uygulama katmanı altyapısı. Request pipeline behavior'ları, DTO base sınıfları ve iş kuralı doğrulama mekanizmaları içerir.

## Pipeline Behaviors

Request'e ilgili interface eklenerek behavior aktif edilir:

| Behavior | Interface | Açıklama |
|---|---|---|
| `AuthorizationBehavior` | `ISecuredRequest` | Rol tabanlı yetkilendirme. SuperAdmin tüm kontrolleri atlar (SuperAdmin gating için `Roles = ["SuperAdmin"]`). |
| `SuperAdminBlockBehavior` | `IBlockedForSuperAdmin` | PlatformAdmin (impersonate etmeden) endpoint'i çağırırsa reddeder. Tenant-user-only operasyonlar için. |
| `RequestValidationBehavior` | otomatik | FluentValidation ile request doğrulama (`ValidationException` → 400) |
| `CachingBehavior` | `ICachableRequest` | Distributed cache. Key'e tenant prefix otomatik eklenir. |
| `CacheRemovingBehavior` | `ICacheRemoverRequest` | Cache grubu temizleme |
| `TransactionScopeBehavior` | `ITransactionalRequest` | İşlem başarısız olursa rollback |
| `LoggingBehavior` | `ILoggableRequest` | Request/response log. `ISensitiveRequest` implement edilirse payload "[redacted]" loglanır. |
| `PerformanceBehavior` | `IIntervalRequest` | Yavaş request uyarısı |
| `TenantValidationBehavior` | `ITenantValidationRequest` | Tenant context olmadan request reddedilir |

> **Cache key kuralı:** `ICachableRequest.CacheKey` consuming app'te **benzersiz** olmalı. Framework key'leri `t:<tenantId>:<CacheKey>` formatında prefix'liyor ama farklı handler'lar aynı CacheKey değerini kullanırsa cache çakışması ve deserialization hatası yaşanır. Konvansiyon: `"Products:GetAll"`, `"Products:ById:{id}"`, asla yalın `"Products"`.

> **`ISensitiveRequest`:** Şifre, token, kredi kartı vb. veri taşıyan command'lara uygulayın — `LoggingBehavior` payload'ı redact eder, sadece request tipi loglanır.

## Kullanım

```csharp
// Secured + cached + tenant-validated bir query örneği
public class GetProductsQuery : IRequest<GetListResponse<ProductDto>>,
    ISecuredRequest,
    ICachableRequest,
    ITenantValidationRequest
{
    public string[] Roles => ["Manager", "Admin"];
    public string CacheKey => "Products";
    public string? CacheGroupKey => "Products";
    public bool BypassCache => false;
    public TimeSpan? SlidingExpiration => null;
}
```

## Bağımlılıklar

- `Core.Security` — yetkilendirme ve claim extension'ları
- `Core.MultiTenancy` — ITenantContext ve tenant claim'leri
- `Core.CrossCuttingConcerns.Exception` — AuthorizationException
- `Core.CrossCuttingConcerns.Logging` — LogDetail, LogParameter
