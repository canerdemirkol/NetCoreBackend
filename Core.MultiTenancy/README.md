# Core.MultiTenancy

Multi-tenant infrastructure for SaaS applications. It performs tenant detection in order via JWT claim, HTTP header, and subdomain.

## Tenant Detection (Priority Order)

```
1. JWT claim: "tenant_id"          → Most secure, available after the token is validated
2. HTTP Header: X-Tenant-ID        → For API clients and the development environment
3. Subdomain: acme.yourapp.com     → For the production SaaS URL structure
```

## Components

| Component | Description |
|---|---|
| `Tenant` | The tenant record entity (name, identifier/slug, domain, plan, defaultLocale, isActive) |
| `ITenantContext` | Interface injected into DI for reading the current tenant |
| `TenantContext` | Scoped ITenantContext implementation, reset per request |
| `ITenantService` | The tenant lookup interface that must be implemented in the application |
| `TenantEntitySetter` | `ITenantEntitySetter` implementation — automatically sets TenantId on Add operations |
| `TenantMiddleware` | Middleware that resolves the tenant on every request. Returns 401 when tenant_id is present in the JWT but the tenant does not exist in the DB. |
| `TenantClaimTypes` | Claim key constants (tenant_id, is_super_admin, is_impersonating) |

## Setup

```csharp
// Program.cs
builder.Services.AddMultiTenancy();
// → ITenantContext, TenantContext, and ITenantEntitySetter are registered automatically

builder.Services.AddScoped<ITenantService, YourTenantService>();

app.UseAuthentication();
app.UseMultiTenancy();   // must come AFTER UseAuthentication
app.UseAuthorization();
```

> **Why does middleware order matter?**
> `TenantMiddleware`'s primary source is the `tenant_id` claim in the JWT. This claim can only
> be read via `HttpContext.User` after `UseAuthentication()` has run. If the order is
> reversed, `User.Claims` stays empty, the middleware falls straight through to the
> header/subdomain fallbacks, and even logged-in users are routed to the wrong tenant (or to no tenant at all).

`AddMultiTenancy()` registers the following:
- `TenantContext` (scoped)
- `ITenantContext` → `TenantContext` (scoped)
- `ITenantEntitySetter` → `TenantEntitySetter` (scoped) — no need to register it separately

## Tenant Entity

```csharp
public class Tenant : Entity<Guid>
{
    public string Name { get; set; }
    public string Identifier { get; set; }   // slug: "acme"
    public string? Domain { get; set; }
    public bool IsActive { get; set; }
    public TenantPlanType PlanType { get; set; }
    public string? DefaultLocale { get; set; }  // "tr", "de" — fallback when Accept-Language is absent
}
```

`DefaultLocale`: When the client does not send an `Accept-Language` header, `LocalizationMiddleware` uses this value as a fallback.

> **`Identifier` must be unique.** The framework does not perform a code-level uniqueness check; the DB constraint
> must be added in the consuming app's `DbContext` configuration:
> ```csharp
> modelBuilder.Entity<Tenant>().HasIndex(t => t.Identifier).IsUnique();
> // Same for Domain (Domain is nullable, multi-tenant)
> modelBuilder.Entity<Tenant>()
>     .HasIndex(t => t.Domain)
>     .IsUnique()
>     .HasFilter("[Domain] IS NOT NULL");
> ```
> Without this constraint, two Tenant records with the `acme` slug can be created, and `GetBySlugAsync`
> returns an ambiguous result.

## SuperAdmin

A user with `is_super_admin: true` and `tenant_id: null` in the JWT can access all tenants' data (the EF Core global filter is bypassed). To switch to a specific tenant, an impersonation token is used.

Detailed documentation: [TENANT.md](../TENANT.md)
