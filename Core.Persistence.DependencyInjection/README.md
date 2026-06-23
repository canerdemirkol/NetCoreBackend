# Core.Persistence.DependencyInjection

DI registration for the service that automatically applies database migrations.

## Installation

```csharp
// Program.cs
builder.Services.AddDbMigrationApplier<AppDbContext>();
```

`DbMigrationApplierManager<TDbContext>` is registered as transient under both `IDbMigrationApplierService` and `IDbMigrationApplierService<TDbContext>`. When the factories are invoked, the DbContext is resolved as scoped from the application's actual `IServiceProvider`.

## Applying Migrations

To run the service, use the [`Core.Persistence.WebApi`](../Core.Persistence.WebApi/README.md) extension method:

```csharp
app.UseDbMigrationApplier();
```

This automatically runs pending migrations when the application starts (`Database.Migrate()`).
