# Core.Persistence.DependencyInjection

Veritabanı migration'larını otomatik uygulayan servisin DI kaydı.

## Kurulum

```csharp
// Program.cs
builder.Services.AddDbMigrationApplier<AppDbContext>();
```

`DbMigrationApplierManager<TDbContext>`, hem `IDbMigrationApplierService` hem de `IDbMigrationApplierService<TDbContext>` olarak transient kaydedilir. DbContext, factory'ler çağrıldığında uygulamanın gerçek `IServiceProvider`'ından scoped olarak çözülür.

## Migration Uygulama

Servisi çalıştırmak için [`Core.Persistence.WebApi`](../Core.Persistence.WebApi/README.md) extension metodunu kullanın:

```csharp
app.UseDbMigrationApplier();
```

Bu, uygulama başlarken bekleyen migration'ları otomatik olarak çalıştırır (`Database.Migrate()`).
