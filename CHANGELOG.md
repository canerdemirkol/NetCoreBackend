# Changelog

All notable changes to this project will be documented in this file.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)  
Versioning: [Semantic Versioning](https://semver.org/)

---

## [3.0.0] - 2026-06-12

### Core.Security 3.0.0

#### Breaking Changes

- **`ITokenHelper<TUserId, TOperationClaimId, TRefreshTokenId>` has a new method: `CreateAdminRefreshToken`.**

  Any custom implementation of `ITokenHelper` must add this method:

  ```csharp
  AdminRefreshToken<TRefreshTokenId, TUserId> CreateAdminRefreshToken(PlatformAdmin<TUserId> admin, string ipAddress);
  ```

  `JwtHelper` already implements it. If you are injecting (not implementing) `ITokenHelper`, no action is needed.

#### Added

- **`AdminRefreshToken<TId, TAdminId>` entity** — refresh token for `PlatformAdmin`. Extends `Entity<TId>` instead of `TenantEntity<TId>` because platform admins are not tenant-scoped. Holds `AdminId` in place of `UserId`. Carries the same rotation/revocation fields (`RevokedDate`, `RevokedByIp`, `ReplacedByToken`, `ReasonRevoked`) and computed properties (`IsExpired`, `IsRevoked`, `IsActive`) as `RefreshToken`.

- **`ITokenHelper.CreateAdminRefreshToken(PlatformAdmin<TUserId> admin, string ipAddress)`** — issues a refresh token for a `PlatformAdmin`. Uses `_tokenOptions.RefreshTokenTtlDays` for expiration, same as `CreateRefreshToken` for regular users.

---

## [2.0.0] - 2026-06-11

### Core.Security 2.0.0

#### Breaking Changes

- **`PlatformAdmin<TId>` base class changed from `AuditableEntity<TId>` → `Entity<TId>`.**

  Platform admins are not application users — there is no meaningful "who created this admin" in the same way as tenant-user audit. Audit columns (`CreatedAt`, `UpdatedAt`, `CreatedById`, `UpdatedById`, `DeletedById`) are removed from the `PlatformAdmins` table.

  **Migration guide:**

  ```bash
  dotnet ef migrations add RemovePlatformAdminAuditColumns
  dotnet ef database update
  ```

  The migration will drop `CreatedAt`, `UpdatedAt`, `CreatedById`, `UpdatedById`, `DeletedById` from the `PlatformAdmins` table. No data in other tables is affected.

---

### Core.Application 2.0.0

#### Breaking Changes

- **`SuperAdminBlockBehavior<TRequest, TResponse>` removed.**

  Previously, this pipeline behavior blocked non-impersonating `PlatformAdmin` tokens from reaching handlers marked `IBlockedForSuperAdmin`. The model has changed: a `PlatformAdmin` without an active impersonation session sees **all tenants' data** (no EF Core tenant filter applies); impersonating narrows the view to a single tenant. There is no longer a category of endpoints that PlatformAdmin must be blocked from.

  **Migration guide:** Remove `IBlockedForSuperAdmin` from any handler that implements it. The interface no longer exists and will cause a compile error.

- **`IBlockedForSuperAdmin` marker interface removed.**

  Consumed only by the now-removed `SuperAdminBlockBehavior`. Any handler implementing this interface will fail to compile — remove the interface from the handler's declaration.

#### Changed

- **`AddNArchitecturePipelineBehaviors` no longer registers `SuperAdminBlockBehavior`.**

  No action needed if `AddNArchitecturePipelineBehaviors()` is called (registration is gone automatically). If the behavior was registered manually, remove it.

---

## [1.1.1] - 2026-06-10

### Core.Security 1.1.1

#### Added

- **`OperationClaim.Description`** — optional `string?` field describing what the claim grants. Nullable and backward-compatible; existing records without a description remain valid.

---

### Core.Persistence 1.1.1

#### Fixed

- **`ExecuteStoredProcedureAsync` is now provider-agnostic.** Previously hardcoded Oracle `BEGIN {procedure}; END;` syntax. Now detects the active EF Core provider at runtime via `Context.Database.ProviderName` and generates the correct syntax:
  - SQL Server → `EXEC {procedure}`
  - PostgreSQL → `CALL {procedure}`
  - Oracle / other → `BEGIN {procedure}; END;` (unchanged fallback)

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
