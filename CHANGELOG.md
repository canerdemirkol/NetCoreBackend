# Changelog

All notable changes to this project will be documented in this file.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)  
Versioning: [Semantic Versioning](https://semver.org/)

---

## [1.1.0] - 2026-06-10

### Core.Persistence 1.1.0

#### Added

- **`IEntityAudit` interface** — declares `CreatedById`, `UpdatedById`, `DeletedById` (`Guid?`) for user-level audit tracking. All nullable so system operations (background jobs, migrations) without an authenticated user are handled gracefully.

- **`ICurrentUserService` interface** — abstraction for resolving the authenticated user's ID. Implement in the consuming application (e.g. via `IHttpContextAccessor` + claim reading) and register as scoped. Returns `null` when no user is authenticated.

- **`AuditableEntity<TId>` base class** — opt-in base that extends `Entity<TId>` and implements `IEntityAudit`. For entities that need audit but no tenant isolation.

- **`AuditableTenantEntity<TId>` base class** — extends `TenantEntity<TId>` and implements `IEntityAudit`. For entities that need both tenant isolation and audit tracking.

- **`EfRepositoryBase` populates audit fields automatically** — accepts optional `ICurrentUserService` in the constructor (backward-compatible; existing code without it continues to work). Audit fields are set only when the entity implements `IEntityAudit`. `CreatedById` is set on Add, `UpdatedById` on Update, `DeletedById` on soft-delete (including cascade).

#### Migration guide

1. For entities that need audit, change the base class:
   ```csharp
   // before
   public class Product : TenantEntity<Guid> { ... }
   // after
   public class Product : AuditableEntity<Guid> { ... }
   // or implement IEntityAudit directly if already using a custom base
   ```
2. Register your `ICurrentUserService` implementation in DI:
   ```csharp
   services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();
   ```
3. Add an EF Core migration only for entities that now extend `AuditableEntity<TId>`.

---

## [1.1.0] - 2026-06-10

### Core.Security 1.1.0

#### Changed

- **`User<TId>`** base class changed from `TenantEntity<TId>` → `AuditableTenantEntity<TId>`.
  Adds `CreatedById`, `UpdatedById`, `DeletedById` columns to the Users table.

- **`UserOperationClaim<TId>`** base class changed from `TenantEntity<TId>` → `AuditableTenantEntity<TId>`.
  Adds audit columns to the UserOperationClaims table. Enables tracking of who granted or revoked a permission.

- **`OperationClaim<TId>`** base class changed from `Entity<TId>` → `AuditableEntity<TId>`.
  Adds audit columns to the OperationClaims table. Enables tracking of who created or deleted a system permission definition.

- **`PlatformAdmin<TId>`** base class changed from `Entity<TId>` → `AuditableEntity<TId>`.
  Adds audit columns to the PlatformAdmins table. Tracks who created or deleted a platform-level administrator — critical for security audit trail.

#### Not changed (intentional)

- `RefreshToken` — system-managed; already tracks `CreatedByIp`/`RevokedByIp`; `CreatedById` would duplicate `UserId`.
- `EmailAuthenticator` — user sets up their own 2FA; lifecycle tracked via `ActivationKey`/`ActivationKeyExpiresAt`/`ActivationKeyConsumedAt`.
- `OtpAuthenticator` — same reasoning as `EmailAuthenticator`.

#### Migration guide

Add an EF Core migration in the consuming application to apply the new columns:

```bash
dotnet ef migrations add AddAuditFieldsToSecurityEntities
dotnet ef database update
```

This adds three nullable `Guid` columns (`CreatedById`, `UpdatedById`, `DeletedById`) to the
following tables: **Users**, **UserOperationClaims**, **OperationClaims**, **PlatformAdmins**.

Existing rows will have `NULL` in these columns — no data loss, no required backfill.

To populate audit fields going forward, register `ICurrentUserService` (from `Core.Persistence`) in the consuming application:

```csharp
services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();
```

---

## [2.0.0] - 2026-06-09

### Core.MultiTenancy 2.0.0

#### Breaking Changes

- **`ModelBuilderTenantExtensions.ApplyTenantFilters` removed.**

  `Expression.Constant(tenantContext)` captures a single `ITenantContext` instance at model-build time. EF Core caches the compiled query — all subsequent requests filter against the first request's tenant, a critical per-request isolation bug.

  **Migration:** Replace with a closure-based `HasQueryFilter` in `OnModelCreating`:

  ```csharp
  protected override void OnModelCreating(ModelBuilder builder)
  {
      builder.Entity<MyEntity>().HasQueryFilter(e =>
          (_tenantContext.IsSuperAdmin || e.TenantId == _tenantContext.TenantId)
          && e.DeletedDate == null);

      base.OnModelCreating(builder);
  }
  ```

  EF Core re-evaluates the closure on every query because `_tenantContext` is captured via the scoped `DbContext` instance. See `TENANT.md` — *EF Core Global Query Filter Setup* for details.

---

## [1.0.1] - 2026-06-09

### Core.MultiTenancy 1.0.1

#### Fixed

- `TenantMiddleware`: returns 401 when JWT carries a `tenant_id` claim but the tenant no longer exists in the database (deleted tenant).
- `TenantValidationBehavior`: now reads from `ITenantContext` instead of raw JWT claims via `IHttpContextAccessor`, correctly reflecting the tenant state resolved by middleware.

### Core.Persistence 1.0.1

#### Fixed

- `EfRepositoryBase.ApplyIncludeDeleted`: `withDeleted: true` now strips the soft-delete filter while preserving tenant isolation. Previously, `IgnoreQueryFilters()` removed both the soft-delete and tenant filters simultaneously.

---

## [1.0.0] - 2026-06-08

Initial release.
