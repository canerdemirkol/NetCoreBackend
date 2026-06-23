# Core.Persistence.WebApi

A middleware extension that automatically applies EF Core migrations at application startup.

## Installation

```csharp
// Program.cs
// 1. Register the migration applier (Core.Persistence.DependencyInjection)
builder.Services.AddDbMigrationApplier<AppDbContext>();

// 2. Run it when the application starts
app.UseDbMigrationApplier();
```

## How It Works

`ApplicationBuilderDbMigrationApplierExtensions.UseDbMigrationApplier()`:

1. Resolves all `IDbMigrationApplierService` implementations from DI
2. Calls `Initialize()` on each of them
3. `Initialize()` → `DbContext.Database.Migrate()` (relational) or `EnsureCreated()` (in-memory) → pending migrations are applied

## Caution

- This extension runs migrations on every startup
- In production, it is safer to apply migrations separately through a CI/CD pipeline
- It is ideal for easy setup in a development environment
