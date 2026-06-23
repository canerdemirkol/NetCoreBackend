# Core.Application

CQRS- and MediatR-based application layer infrastructure. Includes request pipeline behaviors, DTO base classes, and business rule validation mechanisms.

## Pipeline Behaviors

A behavior is activated by adding the relevant interface to the request:

| Behavior | Interface | Description |
|---|---|---|
| `AuthorizationBehavior` | `ISecuredRequest` | Role-based authorization. SuperAdmin bypasses all checks (for SuperAdmin gating use `Roles = ["SuperAdmin"]`). |
| `SuperAdminBlockBehavior` | `IBlockedForSuperAdmin` | Rejects the request if a PlatformAdmin calls the endpoint (without impersonating). For tenant-user-only operations. |
| `RequestValidationBehavior` | automatic | Request validation via FluentValidation (`ValidationException` → 400) |
| `CachingBehavior` | `ICachableRequest` | Distributed cache. The tenant prefix is added to the key automatically. |
| `CacheRemovingBehavior` | `ICacheRemoverRequest` | Clears a cache group |
| `TransactionScopeBehavior` | `ITransactionalRequest` | Rolls back if the operation fails |
| `LoggingBehavior` | `ILoggableRequest` | Request/response logging. If `ISensitiveRequest` is implemented, the payload is logged as "[redacted]". |
| `PerformanceBehavior` | `IIntervalRequest` | Slow request warning |
| `TenantValidationBehavior` | `ITenantValidationRequest` | The request is rejected without a tenant context |

> **Cache key rule:** `ICachableRequest.CacheKey` must be **unique** within the consuming app. The framework prefixes keys in the `t:<tenantId>:<CacheKey>` format, but if different handlers use the same CacheKey value, you will experience cache collisions and deserialization errors. Convention: `"Products:GetAll"`, `"Products:ById:{id}"`, never a bare `"Products"`.

> **`ISensitiveRequest`:** Apply this to commands carrying data such as passwords, tokens, or credit cards — `LoggingBehavior` redacts the payload and logs only the request type.

## Usage

```csharp
// Example of a secured + cached + tenant-validated query
public class GetProductsQuery : IRequest<GetListResponse<ProductDto>>,
    ISecuredRequest,
    ICachableRequest,
    ITenantValidationRequest
{
    public string[] Roles => ["Manager", "Admin"];
    // CacheKey must be unique within the query handler — not a bare "Products",
    // use a value that reflects the query + parameter identity (e.g. "Products:GetAll").
    public string CacheKey => "Products:GetAll";
    public string? CacheGroupKey => "Products";
    public bool BypassCache => false;
    public TimeSpan? SlidingExpiration => null;
}
```

## Dependencies

- `Core.Security` — authorization and claim extensions
- `Core.MultiTenancy` — ITenantContext and tenant claims
- `Core.CrossCuttingConcerns.Exception` — AuthorizationException
- `Core.CrossCuttingConcerns.Logging` — LogDetail, LogParameter
