# Core.Outbox.DependencyInjection

A DI registration helper for `Core.Outbox`.

## Usage

```csharp
// Program.cs
using NetCoreBackend.NArchitecture.Core.Outbox.DependencyInjection;

builder.Services.AddOutbox<AppDbContext>(opt =>
{
    opt.BatchSize        = 100;
    opt.MaxAttempts      = 5;
    opt.IdlePollDelay    = TimeSpan.FromSeconds(1);
    opt.BaseRetryDelay   = TimeSpan.FromSeconds(2);
    opt.MaxRetryDelay    = TimeSpan.FromMinutes(10);
});

// The publisher is consumer-specific — implement and register it yourself:
builder.Services.AddScoped<IOutboxPublisher, MyRabbitMqPublisher>();
```

## What Gets Registered

| Service | Lifetime | Description |
|---|---|---|
| `IOutboxStore` | Scoped (TryAdd) | `EfOutboxStore<TDbContext>` — not overridden if the consumer has registered a custom store |
| `OutboxOptions` | Singleton | via `IOptions<T>` |
| `OutboxPublisherWorker` | HostedService | BackgroundService — starts running when the host starts |

## Consumer Responsibilities

1. Register the DbContext with `AddDbContext<TDbContext>(...)`.
2. Call `modelBuilder.ConfigureOutbox()` inside `OnModelCreating`.
3. Register the `IOutboxPublisher` implementation (Scoped is recommended — a new scope per batch).
