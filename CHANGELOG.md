# Changelog

All notable changes to this project will be documented in this file.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)  
Versioning: [Semantic Versioning](https://semver.org/)

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
