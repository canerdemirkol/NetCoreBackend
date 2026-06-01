# Core.Persistence

EF Core tabanlı repository pattern implementasyonu. Soft delete, sayfalama, dinamik filtreleme ve multi-tenant entity desteği içerir.

## Entity Hiyerarşisi

```
Entity<TId>                    ← Id, CreatedDate, UpdatedDate, DeletedDate
    └── TenantEntity<TId>      ← + TenantId (Guid) — tenant-aware entity'ler için
```

## Repository Pattern

```csharp
// Temel repository
public class ProductRepository : EfRepositoryBase<Product, Guid, AppDbContext>
{
    public ProductRepository(AppDbContext context, ITenantEntitySetter? tenantSetter = null)
        : base(context, tenantSetter) { }
}

// Async interface
IAsyncRepository<Product, Guid>

// Sync interface
IRepository<Product, Guid>
```

## Özellikler

| Özellik | Açıklama |
|---|---|
| **Soft Delete** | `DeletedDate` set edilerek soft silinir. Global query filter ile filtrelenir. |
| **Sayfalama** | `ToPaginateAsync(index, size)` — `IPaginate<T>` döner |
| **Dinamik Filtreleme** | `GetListByDynamicAsync(DynamicQuery)` — field, operator, value, logic desteği |
| **Tenant Otomatik Set** | `Add`/`AddRange`'de `ITenantEntity` ise `TenantId` otomatik set edilir |
| **SQL Komutları** | `ExecuteSqlRawAsync`, `ExecuteStoredProcedureAsync`, `ExecuteSqlCommand<TResult>` |

## Tenant Entity Oluşturma

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

## SQL Metodları ve Tenant Güvenliği

### `ExecuteSqlCommand` — Güvenli ✅

`DbSet.FromSqlRaw()` üzerinden çalışır. EF Core, yazdığın SQL'i subquery olarak sarar ve global query filter'ı otomatik uygular:

```sql
-- Yazdığın:
SELECT * FROM Orders WHERE Total > 100

-- EF Core'un ürettiği:
SELECT * FROM (...) AS t WHERE t.TenantId = 'acme-guid' AND t.DeletedDate IS NULL
```

### `ExecuteSqlRawAsync` / `ExecuteStoredProcedureAsync` — Manuel Tenant Filtresi ⚠️

`Database.ExecuteSqlRawAsync()` doğrudan veritabanına gider, EF Core filtrelerini tamamen bypass eder. `TenantEntity` üzerinde çağrıldığında **tenant context yoksa exception fırlatır** (SuperAdmin hariç).

```csharp
// Concrete repository içinde CurrentTenantId kullan:
public async Task<int> BulkArchiveAsync(DateTime before)
{
    return await ExecuteSqlRawAsync(
        "UPDATE Orders SET Archived = 1 WHERE TenantId = @p0 AND CreatedDate < @p1",
        [CurrentTenantId, before]   // ← CurrentTenantId protected property'den gelir
    );
}

// Stored procedure: proc kendi içinde @tenantId parametresi almalı
public async Task<int> ArchiveOldOrdersAsync(DateTime before)
{
    return await ExecuteStoredProcedureAsync(
        "sp_ArchiveOldOrders(@p0, @p1)",
        [CurrentTenantId, before]
    );
}
```

`CurrentTenantId` → `EfRepositoryBase`'de `protected Guid? CurrentTenantId => TenantSetter?.CurrentTenantId;` olarak tanımlıdır.

## Dinamik Query

```json
{
  "filter": { "field": "name", "operator": "contains", "value": "phone" },
  "sort": [{ "field": "createdDate", "dir": "desc" }]
}
```

Desteklenen operatörler: `eq`, `neq`, `lt`, `lte`, `gt`, `gte`, `isnull`, `isnotnull`, `startswith`, `endswith`, `contains`, `doesnotcontain`, `in`, `between`
