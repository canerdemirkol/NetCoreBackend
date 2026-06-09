# Core.Persistence

EF Core tabanlı repository pattern implementasyonu. Soft delete, sayfalama, dinamik filtreleme ve multi-tenant entity desteği içerir.

## Entity Hiyerarşisi

```
Entity<TId>                    ← Id, CreatedDate, UpdatedDate, DeletedDate
    └── TenantEntity<TId>      ← + TenantId (Guid) — tenant-aware entity'ler için
```

## Repository Pattern

```csharp
// Tenant-aware entity için: tenantSetter ZORUNLU.
// AddMultiTenancy() çağrılmamışsa ITenantEntity Add'inde hard error fırlar.
public class OrderRepository : EfRepositoryBase<Order, Guid, AppDbContext>
{
    public OrderRepository(AppDbContext context, ITenantEntitySetter tenantSetter)
        : base(context, tenantSetter) { }
}

// Tenant-bağımsız entity için (Country gibi): tenantSetter null geçilebilir.
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

## Özellikler

| Özellik | Açıklama |
|---|---|
| **Soft Delete** | `DeletedDate` set edilerek soft silinir. Global query filter ile filtrelenir. Cascade soft-delete tenant-aware (başka tenant'ın row'una dokunulmaz). `withDeleted: true` silinmiş kayıtları getirir ancak tenant izolasyonunu korur — başka tenant'ın verisi görünmez. |
| **Sayfalama** | `ToPaginateAsync(index, size, from)` — `IPaginate<T>` döner. `size <= 0` veya `from > index` `ArgumentOutOfRangeException` fırlatır. |
| **Dinamik Filtreleme** | `GetListByDynamicAsync(DynamicQuery)` — field, operator, value, logic desteği. `[NotFilterable]` ile işaretli property'ler reddedilir. |
| **Tenant Otomatik Set** | `Add`/`AddRange`'de `ITenantEntity` ise `TenantId` otomatik set edilir; `ITenantEntitySetter` register edilmemişse hard error. |
| **SQL Komutları** | `ExecuteSqlRawAsync`, `ExecuteStoredProcedureAsync`, `ExecuteSqlCommand<TResult>` |

## NotFilterable — Hassas property'leri whitelist'ten çıkar

Dinamik query'de kullanıcı-sağlı `field` ismi `typeof(TEntity)` üzerinde public property arar. Hassas alanlara (parola hash'i, internal audit field) erişimi blocklamak için `[NotFilterable]` attribute'unu kullan:

```csharp
public class User : TenantEntity<Guid>
{
    public string Email { get; set; }
    [NotFilterable] public byte[] PasswordHash { get; set; }
    [NotFilterable] public byte[] PasswordSalt { get; set; }
}
```

`User` ve `PlatformAdmin` framework içinde halihazırda işaretlidir.

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

### `ExecuteUpdateAsync` / `ExecuteDeleteAsync` — Bulk operations, tenant-safe ✅

EF Core 7+ bulk update/delete API'sinin (`UpdateSettersBuilder<T>`) tenant-aware wrapper'ı. Predicate `Query()` üzerinden zincirlendiği için EF Core'un global query filter'ı otomatik uygulanır; ek olarak `GuardTenantContext()` raw-SQL path'leriyle aynı tenant context kontrolünü yapar.

```csharp
// Bulk update — sadece current tenant'ın kayıtları etkilenir
await _orderRepo.ExecuteUpdateAsync(
    predicate: o => o.Status == OrderStatus.Pending && o.CreatedDate < cutoff,
    setPropertyCalls: setters => setters.SetProperty(o => o.Status, OrderStatus.Expired),
    cancellationToken: ct);

// Bulk delete — soft-delete istiyorsan ExecuteUpdate ile DeletedDate yaz
await _orderRepo.ExecuteDeleteAsync(
    predicate: o => o.Status == OrderStatus.Archived && o.UpdatedDate < cutoff,
    cancellationToken: ct);
```

`Update`/`Delete`'in tek-row paterninden farkı: payload pre-load gerekmez, tek SQL statement gönderilir, bellek efficient. Kullanırken predicate'in TENANT scope'unda olduğundan emin ol — yanlış bir predicate bütün tenant'ın verisini etkileyebilir.

## Dinamik Query

```json
{
  "filter": { "field": "name", "operator": "contains", "value": "phone" },
  "sort": [{ "field": "createdDate", "dir": "desc" }]
}
```

Desteklenen operatörler: `eq`, `neq`, `lt`, `lte`, `gt`, `gte`, `isnull`, `isnotnull`, `startswith`, `endswith`, `contains`, `doesnotcontain`, `in`, `between`
