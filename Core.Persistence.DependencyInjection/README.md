# Core.Persistence.DependencyInjection

Veritabanı migration'larını otomatik uygulayan servisin DI kaydı.

## Kurulum

```csharp
// Program.cs
builder.Services.AddDbMigrationApplier<AppDbContext>(
    sp => sp.GetRequiredService<AppDbContext>()
);
```

`DbMigrationApplierManager<TDbContext>`, hem `IDbMigrationApplierService` hem de `IDbMigrationApplierService<TDbContext>` olarak transient kaydedilir. Factory parametresi servisi DI container'dan çekmek için kullanılır.

## Migration Uygulama

Servisi çalıştırmak için [`Core.Persistence.WebApi`](../Core.Persistence.WebApi/README.md) extension metodunu kullanın:

```csharp
app.UseDbMigrationApplier();
```

Bu, uygulama başlarken bekleyen migration'ları otomatik olarak çalıştırır (`Database.Migrate()`).
