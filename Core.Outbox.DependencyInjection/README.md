# Core.Outbox.DependencyInjection

`Core.Outbox`'a DI kayıt yardımcısı.

## Kullanım

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

// Publisher consumer-spesifik — kendin implement et ve register et:
builder.Services.AddScoped<IOutboxPublisher, MyRabbitMqPublisher>();
```

## Ne Register Ediliyor

| Service | Lifetime | Açıklama |
|---|---|---|
| `IOutboxStore` | Scoped (TryAdd) | `EfOutboxStore<TDbContext>` — consumer custom store register etmişse override edilmez |
| `OutboxOptions` | Singleton | `IOptions<T>` üzerinden |
| `OutboxPublisherWorker` | HostedService | BackgroundService — host başladığında çalışmaya başlar |

## Consumer Sorumlulukları

1. `AddDbContext<TDbContext>(...)` ile DbContext'i kayıt et.
2. `OnModelCreating` içinde `modelBuilder.ConfigureOutbox()` çağır.
3. `IOutboxPublisher` implementasyonunu register et (Scoped önerilir — her batch yeni scope).
