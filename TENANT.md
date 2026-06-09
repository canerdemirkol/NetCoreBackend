# Multi-Tenancy Architecture

This document describes the multi-tenancy infrastructure provided by `Core.MultiTenancy`.

---

## Strategy: Shared DB + Tenant Isolation via EF Core Global Query Filter

Each tenant's data lives in the **same database** but is isolated by a `TenantId` column on every tenant-aware entity. The isolation is enforced automatically at the EF Core query level — developers never write `WHERE TenantId = ...` manually.

---

## Tenant Resolution — Priority Order

Every HTTP request goes through `TenantMiddleware`, which resolves the current tenant in this order:

```
1. JWT Claim  →  token contains "tenant_id"         (most secure — tamper-proof)
2. HTTP Header →  X-Tenant-ID: acme                 (for dev/test or API clients)
3. Subdomain  →  acme.yourapp.com                   (production SaaS URL)
```

If none of these resolves a tenant, the request continues without a tenant context (suitable for public endpoints like health checks).

> **Deleted tenant:** If the JWT contains a `tenant_id` claim but the tenant no longer exists in the database, the middleware returns **401** and stops the request — the token is considered stale. The client must re-authenticate.

---

## Component Overview

```
Core.MultiTenancy/
├── Entities/
│   └── Tenant.cs                  ← Tenant record (name, slug, domain, plan, isActive)
├── Abstractions/
│   ├── ITenantContext.cs          ← Read-only view of current tenant (injected anywhere)
│   └── ITenantService.cs          ← Implement this in your app to look up tenants from DB
├── Context/
│   └── TenantContext.cs           ← Scoped, mutable implementation of ITenantContext
├── Extensions/
├── Middleware/
│   └── TenantMiddleware.cs        ← Resolves tenant per-request (JWT → Header → Subdomain)
├── Constants/
│   └── TenantClaimTypes.cs        ← Claim key constants (tenant_id, is_super_admin, ...)
├── Exceptions/
│   ├── TenantNotFoundException.cs
│   └── TenantNotActiveException.cs
└── DependencyInjection/
    └── TenantServiceRegistration.cs   ← AddMultiTenancy() + UseMultiTenancy()
```

---

## Making an Entity Tenant-Aware

Extend `TenantEntity<TId>` instead of `Entity<TId>`:

```csharp
// Before (global entity)
public class Product : Entity<Guid>
{
    public string Name { get; set; }
}

// After (tenant-isolated entity)
public class Product : TenantEntity<Guid>
{
    public string Name { get; set; }
}
```

`TenantEntity<TId>` adds a `Guid TenantId` property and implements `ITenantEntity`.  
`EfRepositoryBase` automatically sets `TenantId` on `Add` / `AddRange` operations.

### Which entities should be TenantEntity?

```
Tenant'a ait veri → TenantEntity    (Orders, Products, Invoices, Users, ...)
Platform'un ortak verisi → Entity   (Countries, Currencies, OperationClaims, Tenant itself, ...)
```

`Core.Security` entities are already configured correctly out of the box:

| Entity | Base | Note |
|---|---|---|
| `User<TId>` | `TenantEntity` | Same email can exist in multiple tenants |
| `RefreshToken<TId, TUserId>` | `TenantEntity` | Scoped per tenant for bulk revocation |
| `UserOperationClaim<TId, TUserId, TOperationClaimId>` | `TenantEntity` | Scoped per tenant |
| `EmailAuthenticator<TId>` | `TenantEntity` | Scoped per tenant |
| `OtpAuthenticator<TId>` | `TenantEntity` | Scoped per tenant |
| `OperationClaim<TId>` | `Entity` | Platform-wide roles, shared across tenants |

---

## Tenant Entity Fields

| Field | Type | Description |
|---|---|---|
| `Name` | `string` | Display name ("Acme Corp") |
| `Identifier` | `string` | URL slug ("acme") — used for subdomain and header resolution |
| `Domain` | `string?` | Custom domain ("app.acmecorp.com") |
| `IsActive` | `bool` | Inactive tenants are rejected with 403 |
| `PlanType` | `TenantPlanType` | Free / Basic / Pro / Enterprise |
| `DefaultLocale` | `string?` | BCP 47 fallback locale ("tr", "de") — used when client sends no Accept-Language header |

---

## EF Core Global Query Filter Setup

In your application's `DbContext`, write a per-entity closure in `OnModelCreating`. EF Core re-evaluates the closure on every query because `_tenantContext` is captured via the scoped `DbContext` instance:

```csharp
public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions options, ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Product>().HasQueryFilter(e =>
            (_tenantContext.IsSuperAdmin || e.TenantId == _tenantContext.TenantId)
            && e.DeletedDate == null);

        base.OnModelCreating(builder);
    }
}
```

Filter behavior:
- **SuperAdmin** → no tenant restriction (sees all tenants), soft-delete still applied
- **Tenant user** → `WHERE TenantId = @currentTenantId AND DeletedDate IS NULL`
- **No tenant context** (public endpoints, health checks) → `TenantId == null` evaluates to `false`, empty set returned

> **Why not a generic extension method?** `Expression.Constant(tenantContext)` captures a single
> `ITenantContext` instance at model-build time. EF Core caches the compiled query, so all subsequent
> requests would evaluate against the first request's tenant — a critical security flaw. The closure
> approach works because EF Core recognizes the `DbContext`-captured reference and re-reads it per query.

---

## JWT Claim Structure

> Note: `is_super_admin` and `is_impersonating` claims are only emitted when their value is `true` —
> they are **absent** (not `false`) on normal tenant user tokens. The `IsSuperAdmin()` / `IsImpersonating()`
> extension methods treat absent claims as `false`.

### Normal Tenant User
```json
{
  "sub": "user-guid",
  "email": "user@acme.com",
  "role": ["Manager"],
  "tenant_id": "acme-guid"
}
```

### Super Admin (platform owner, sees all tenants)
```json
{
  "sub": "superadmin-guid",
  "email": "admin@platform.com",
  "role": ["SuperAdmin"],
  "is_super_admin": "true"
}
```

### Super Admin Impersonating a Tenant
```json
{
  "sub": "superadmin-guid",
  "email": "admin@platform.com",
  "role": ["SuperAdmin"],
  "tenant_id": "acme-guid",
  "is_super_admin": "true",
  "is_impersonating": "true"
}
```

---

## Creating Tenant-Aware JWT Tokens

```csharp
// Normal tenant user token (tenant_id claim is sourced from user.TenantId)
AccessToken token = _tokenHelper.CreateToken(user, operationClaims);

// PlatformAdmin token (is_super_admin: true, no tenant_id)
AccessToken token = _tokenHelper.CreateAdminToken(admin, operationClaims);

// PlatformAdmin impersonating a tenant
AccessToken token = _tokenHelper.CreateImpersonationToken(admin, operationClaims, targetTenantId);
```

---

## Implementing ITenantService

You must provide an implementation in your application:

```csharp
public class TenantService : ITenantService
{
    private readonly AppDbContext _context;

    public TenantService(AppDbContext context) => _context = context;

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Identifier == slug, ct);

    public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default)
        => _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Domain == domain, ct);
}
```

> Note: Use `IgnoreQueryFilters()` here — tenant lookup must bypass the very filter it sets up.

---

## Registration

```csharp
// Program.cs
builder.Services.AddMultiTenancy();
// Registers: ITenantContext, TenantContext, ITenantEntitySetter (via TenantEntitySetter)
// ITenantEntitySetter is provided automatically — no need to implement it yourself.

builder.Services.AddScoped<ITenantService, TenantService>();

// Middleware order (critical)
app.UseAuthentication();      // JWT must be parsed before tenant resolution
app.UseMultiTenancy();        // resolves tenant from JWT / header / subdomain
app.UseAuthorization();
```

---

## Raw SQL and Tenant Safety

EF Core LINQ methods (GetListAsync, GetAsync, AddAsync, etc.) apply tenant isolation automatically. Raw SQL methods behave differently:

| Method | Sync / Async | EF Core Pipeline | Tenant Filter | Developer Responsibility |
|---|---|---|---|---|
| `ExecuteSqlCommand<T>` | **Sync** | ✅ `DbSet.FromSqlRaw` | Applied automatically | `TResult` must be a mapped entity type (not a DTO) |
| `ExecuteSqlRawAsync` | Async | ❌ `Database` | **Not applied** | Must include `WHERE TenantId = @p0` |
| `ExecuteStoredProcedureAsync` | Async | ❌ `Database` | **Not applied** | Proc must accept `@tenantId`; uses Oracle `BEGIN…END;` syntax |

**`ExecuteSqlCommand<T>` constraint:** The type parameter `TResult` must satisfy `Entity<TEntityId>` — it maps to a DbSet registered in your context. It cannot project to arbitrary DTOs or scalar values. Use it only for SELECT queries that return full entity rows.

**`ExecuteStoredProcedureAsync` database compatibility:** The implementation wraps the call as `BEGIN {procedure}; END;`, which is **Oracle PL/SQL syntax**. For **SQL Server** use `ExecuteSqlRawAsync("EXEC {procedure} @p0", ...)` directly. For **PostgreSQL** use `CALL {procedure}(...)`.

`ExecuteSqlRawAsync` and `ExecuteStoredProcedureAsync` throw `InvalidOperationException` if called on a `TenantEntity` without an active tenant context (SuperAdmin is exempt). Use `CurrentTenantId` from `EfRepositoryBase` to build safe queries:

```csharp
public async Task<int> BulkArchiveAsync(DateTime before)
{
    return await ExecuteSqlRawAsync(
        "UPDATE Orders SET Archived = 1 WHERE TenantId = @p0 AND CreatedDate < @p1",
        [CurrentTenantId, before]
    );
}
```

---

## Super Admin Scenarios

| Scenario | How |
|---|---|
| List all tenants | `IgnoreQueryFilters()` in query — SuperAdmin bypasses global filter |
| View a specific tenant's data | Issue impersonation token, all queries scoped to that tenant |
| Cross-tenant reports | Direct query with `IgnoreQueryFilters()` in a dedicated service |
| Switch back to "no tenant" | Issue new SuperAdmin token without `tenant_id` |

---

## MVC Core Client Integration

If you have an MVC Core frontend consuming this API:

```csharp
// MVC adds the tenant to every API request automatically
services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri("https://api.yourapp.com");
}).AddHttpMessageHandler<TenantHeaderHandler>();

// TenantHeaderHandler reads subdomain from current HttpContext
public class TenantHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        string host = _httpContextAccessor.HttpContext?.Request.Host.Host ?? string.Empty;
        string[] parts = host.Split('.');
        if (parts.Length >= 3)
            request.Headers.Add("X-Tenant-ID", parts[0]);

        return base.SendAsync(request, ct);
    }
}
```

After login, the API returns a JWT containing `tenant_id`. The MVC app stores this token and subsequent requests are resolved via JWT claim (Priority 1) — no header injection needed.

---

## Middleware Pipeline (ASP.NET Core)

```
UseRouting()
UseAuthentication()          ← JWT parsed, user.Claims populated
UseMultiTenancy()            ← reads JWT claim → header → subdomain → sets ITenantContext (+ DefaultLocale)
UseLocalizationMiddleware()  ← reads Accept-Language header; falls back to ITenantContext.DefaultLocale
UseAuthorization()           ← SuperAdmin bypass happens here via IsSuperAdmin() extension
MapControllers()
```

> `UseLocalizationMiddleware()` must come after `UseMultiTenancy()` so that `ITenantContext.DefaultLocale` is already populated when the locale fallback logic runs.
