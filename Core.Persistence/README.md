# Core.Persistence

An EF Core-based repository pattern implementation. Includes soft delete, pagination, dynamic filtering, and multi-tenant entity support.

## Entity Hierarchy

```
Entity<TId>                    ← Id, CreatedDate, UpdatedDate, DeletedDate
    └── TenantEntity<TId>      ← + TenantId (Guid) — for tenant-aware entities
```

## Repository Pattern

```csharp
// For a tenant-aware entity: tenantSetter is REQUIRED.
// If AddMultiTenancy() has not been called, a hard error is thrown on ITenantEntity Add.
public class OrderRepository : EfRepositoryBase<Order, Guid, AppDbContext>
{
    public OrderRepository(AppDbContext context, ITenantEntitySetter tenantSetter)
        : base(context, tenantSetter) { }
}

// For a tenant-independent entity (such as Country): tenantSetter can be passed as null.
public class CountryRepository : EfRepositoryBase<Country, int, AppDbContext>
{
    public CountryRepository(AppDbContext context)
        : base(context, tenantSetter: null) { }
}

// Async interface
IAsyncRepository<Product, Guid>

// Sync interface
IRepository<Product, Guid>
```

## Features

| Feature | Description |
|---|---|
| **Soft Delete** | An entity is soft-deleted by setting `DeletedDate`. It is filtered out by the global query filter. Cascade soft-delete is tenant-aware (another tenant's rows are not touched). `withDeleted: true` retrieves deleted records but preserves tenant isolation — another tenant's data is not visible. |
| **Pagination** | `ToPaginateAsync(index, size, from)` — returns `IPaginate<T>`. `size <= 0` or `from > index` throws `ArgumentOutOfRangeException`. |
| **Dynamic Filtering** | `GetListByDynamicAsync(DynamicQuery)` — supports field, operator, value, logic. Properties marked with `[NotFilterable]` are rejected. |
| **Automatic Tenant Set** | On `Add`/`AddRange`, if the entity is an `ITenantEntity`, `TenantId` is set automatically; if `ITenantEntitySetter` is not registered, a hard error is thrown. |
| **SQL Commands** | `ExecuteSqlRawAsync`, `ExecuteStoredProcedureAsync`, `ExecuteSqlCommand<TResult>` |

## NotFilterable — Exclude sensitive properties from the whitelist

In a dynamic query, the user-supplied `field` name looks up a public property on `typeof(TEntity)`. To block access to sensitive fields (password hash, internal audit fields), use the `[NotFilterable]` attribute:

```csharp
public class User : TenantEntity<Guid>
{
    public string Email { get; set; }
    [NotFilterable] public byte[] PasswordHash { get; set; }
    [NotFilterable] public byte[] PasswordSalt { get; set; }
}
```

`User` and `PlatformAdmin` are already marked within the framework.

## Creating a Tenant Entity

```csharp
// Tenant-aware entity
public class Order : TenantEntity<Guid>
{
    public decimal Total { get; set; }
}

// Normal (cross-tenant) entity
public class Country : Entity<int>
{
    public string Name { get; set; }
}
```

## SQL Methods and Tenant Safety

### `ExecuteSqlCommand` — Safe ✅

Runs via `DbSet.FromSqlRaw()`. EF Core wraps the SQL you write as a subquery and applies the global query filter automatically:

```sql
-- What you write:
SELECT * FROM Orders WHERE Total > 100

-- What EF Core produces:
SELECT * FROM (...) AS t WHERE t.TenantId = 'acme-guid' AND t.DeletedDate IS NULL
```

### `ExecuteSqlRawAsync` / `ExecuteStoredProcedureAsync` — Manual Tenant Filter ⚠️

`Database.ExecuteSqlRawAsync()` goes directly to the database and completely bypasses EF Core filters. When called on a `TenantEntity`, it **throws an exception if there is no tenant context** (except for SuperAdmin).

```csharp
// Use CurrentTenantId inside the concrete repository:
public async Task<int> BulkArchiveAsync(DateTime before)
{
    return await ExecuteSqlRawAsync(
        "UPDATE Orders SET Archived = 1 WHERE TenantId = @p0 AND CreatedDate < @p1",
        [CurrentTenantId, before]   // ← CurrentTenantId comes from a protected property
    );
}

// Stored procedure: the proc must take a @tenantId parameter itself
public async Task<int> ArchiveOldOrdersAsync(DateTime before)
{
    return await ExecuteStoredProcedureAsync(
        "sp_ArchiveOldOrders(@p0, @p1)",
        [CurrentTenantId, before]
    );
}
```

`CurrentTenantId` → defined in `EfRepositoryBase` as `protected Guid? CurrentTenantId => TenantSetter?.CurrentTenantId;`.

### `ExecuteUpdateAsync` / `ExecuteDeleteAsync` — Bulk operations, tenant-safe ✅

A tenant-aware wrapper around the EF Core 7+ bulk update/delete API (`UpdateSettersBuilder<T>`). Because the predicate is chained through `Query()`, EF Core's global query filter is applied automatically; in addition, `GuardTenantContext()` performs the same tenant context check as the raw-SQL paths.

```csharp
// Bulk update — only the current tenant's records are affected
await _orderRepo.ExecuteUpdateAsync(
    predicate: o => o.Status == OrderStatus.Pending && o.CreatedDate < cutoff,
    setPropertyCalls: setters => setters.SetProperty(o => o.Status, OrderStatus.Expired),
    cancellationToken: ct);

// Bulk delete — if you want a soft delete, write DeletedDate via ExecuteUpdate
await _orderRepo.ExecuteDeleteAsync(
    predicate: o => o.Status == OrderStatus.Archived && o.UpdatedDate < cutoff,
    cancellationToken: ct);
```

The difference from the single-row pattern of `Update`/`Delete`: no payload pre-load is required, a single SQL statement is sent, and it is memory-efficient. When using it, make sure the predicate is within the TENANT scope — a wrong predicate can affect the entire tenant's data.

## Dynamic Query

```json
{
  "filter": { "field": "name", "operator": "contains", "value": "phone" },
  "sort": [{ "field": "createdDate", "dir": "desc" }]
}
```

Supported operators: `eq`, `neq`, `lt`, `lte`, `gt`, `gte`, `isnull`, `isnotnull`, `startswith`, `endswith`, `contains`, `doesnotcontain`, `in`, `between`
