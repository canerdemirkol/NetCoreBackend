# Core.Application

CQRS ve MediatR tabanlı uygulama katmanı altyapısı. Request pipeline behavior'ları, DTO base sınıfları ve iş kuralı doğrulama mekanizmaları içerir.

## Pipeline Behaviors

Request'e ilgili interface eklenerek behavior aktif edilir:

| Behavior | Interface | Açıklama |
|---|---|---|
| `AuthorizationBehavior` | `ISecuredRequest` | Rol tabanlı yetkilendirme. SuperAdmin tüm kontrolleri atlar. |
| `RequestValidationBehavior` | otomatik | FluentValidation ile request doğrulama |
| `CachingBehavior` | `ICachableRequest` | Distributed cache. Key'e tenant prefix otomatik eklenir. |
| `CacheRemovingBehavior` | `ICacheRemoverRequest` | Cache grubu temizleme |
| `TransactionScopeBehavior` | `ITransactionalRequest` | İşlem başarısız olursa rollback |
| `LoggingBehavior` | `ILoggableRequest` | Request/response log. Log'a `tenant_id` eklenir. |
| `PerformanceBehavior` | `IIntervalRequest` | Yavaş request uyarısı |
| `TenantValidationBehavior` | `ITenantValidationRequest` | Tenant context olmadan request reddedilir |

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
