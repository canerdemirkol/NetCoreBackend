# Core.Persistence.WebApi

Uygulama başlangıcında EF Core migration'larını otomatik uygulayan middleware extension.

## Kurulum

```csharp
// Program.cs
// 1. Migration applier'ı kaydet (Core.Persistence.DependencyInjection)
builder.Services.AddDbMigrationApplier<AppDbContext>();

// 2. Uygulama başlarken çalıştır
app.UseDbMigrationApplier();
```

## Nasıl Çalışır

`ApplicationBuilderDbMigrationApplierExtensions.UseDbMigrationApplier()`:

1. DI'dan tüm `IDbMigrationApplierService` implementasyonlarını resolve eder
2. Her biri için `Initialize()` çağırır
3. `Initialize()` → `DbContext.Database.Migrate()` (relational) veya `EnsureCreated()` (in-memory) → bekleyen migration'lar uygulanır

## Dikkat

- Bu extension, migration'ları her startup'ta çalıştırır
- Production'da migration'ları CI/CD pipeline'ı üzerinden ayrıca uygulamak daha güvenlidir
- Development ortamında kolay kurulum için idealdir
