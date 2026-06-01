# Core.Persistence.DependencyInjection

Veritabanı migration'larını otomatik uygulayan servisin DI kaydı.

## Kurulum

```csharp
// Program.cs
builder.Services.AddDbMigrationApplier<AppDbContext>();
```

`DbMigrationApplierManager<TDbContext>`, `IDbMigrationApplierService` olarak kaydedilir.

## Migration Uygulama

Servisi çalıştırmak için [`Core.Persistence.WebApi`](../Core.Persistence.WebApi/README.md) extension metodunu kullanın:

```csharp
app.UseDbMigrationApplier();
```

Bu, uygulama başlarken bekleyen migration'ları otomatik olarak çalıştırır (`Database.MigrateAsync()`).
