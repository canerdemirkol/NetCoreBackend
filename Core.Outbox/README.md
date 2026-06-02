# Core.Outbox

**Transactional Outbox** pattern implementasyonu. Atomik DB-write + event publish'i tek bir transaction'da kilitler. Distributed event göndermek için "publish ile DB-commit arasında crash olursa ne olur?" sorusunu çözer.

> **TL;DR:** RabbitMQ / Kafka / SNS gibi dış sistemlere event gönderiyorsan, "DB commit OK ama event publish FAIL" senaryosu sistemini kalıcı inconsistent state'e sokar. Outbox bu senaryoyu imkânsız kılar.

---

## 1. Çözülen problem

### ❌ Klasik kırık pattern

```csharp
public async Task PlaceOrderAsync(Order order, CancellationToken ct)
{
    _db.Orders.Add(order);
    await _db.SaveChangesAsync(ct);              // 1. DB commit OK

    // ⚠️ Bu satırda crash olursa (network glitch, container OOM kill, process restart):
    //    - DB'de order var
    //    - RabbitMQ'ya event GİTMEDİ
    //    - Inventory service "OrderPlaced" haberini hiç almıyor
    //    - Sistem kalıcı inconsistent state'te → manuel reconciliation gerekir
    await _rabbit.PublishAsync(new OrderPlacedEvent(order.Id), ct);
}
```

Tersini denersen (önce publish, sonra DB) bu sefer:
- Publish OK ama DB commit fail → consumer'lar olmayan order'a tepki verir → `OrderNotFound` exception fırtınası

`try/catch + retry` ile düzeltmeye çalışmak da çözüm değil: process tamamen ölürse retry mantığı bile çalışmaz.

### ✅ Outbox çözümü

```csharp
public async Task PlaceOrderAsync(Order order, CancellationToken ct)
{
    _db.Orders.Add(order);

    // Outbox satırı DA aynı DbContext'e eklenir → tek transaction
    await _outbox.AppendAsync(new OutboxMessage
    {
        Id            = Guid.NewGuid(),
        EventType     = "Orders.Placed.v1",
        Payload       = JsonSerializer.Serialize(new { order.Id, order.Total, order.CustomerId }),
        CorrelationId = _httpContext.TraceIdentifier,
        OccurredAtUtc = DateTime.UtcNow
    }, ct);

    await _db.SaveChangesAsync(ct);  // Order + outbox ATOMIC commit
}
```

Sonra arka planda `OutboxPublisherWorker` outbox tablosunu polling eder ve consumer'ın yazdığı `IOutboxPublisher`'a teslim eder. Üç senaryo:

| Senaryo | Sonuç |
|---|---|
| DB commit'ten önce crash | İkisi de yazılmaz → kullanıcı 500 alır, retry eder, no inconsistency |
| Commit OK, sonra crash | Outbox satırı persist'tir → worker restart olunca yakalar, RabbitMQ'ya yollar, `ProcessedAtUtc` stamp'ler |
| RabbitMQ down | Worker fail eder, `AttemptCount++`, exponential backoff ile retry, `MaxAttempts` aşılırsa `IsPoisoned = true` → operator inceler. **Event kaybı yok.** |

---

## 2. Akış diyagramı

```
┌─────────────────────────────┐
│ HTTP request / Command      │
│   PlaceOrderCommand         │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────────────────────────┐
│ Handler                                         │
│   _db.Orders.Add(order)                         │
│   _outbox.AppendAsync(orderPlacedMessage)       │
│   await _db.SaveChangesAsync()  ◄── ATOMIC      │
└──────────────┬──────────────────────────────────┘
               │
               ▼
       ┌───────────────┐
       │   Database    │
       │ ┌───────────┐ │       ┌───────────────────────┐
       │ │ Orders    │ │       │ OutboxPublisherWorker │
       │ ├───────────┤ │       │  (BackgroundService)  │
       │ │ Outbox    │◄┼───────┤  - FetchDueAsync      │
       │ └───────────┘ │       │  - PublishAsync       │
       └───────────────┘       │  - MarkProcessed /    │
                               │    RecordFailure      │
                               └──────────┬────────────┘
                                          │
                                          ▼
                               ┌──────────────────────┐
                               │  IOutboxPublisher    │
                               │  (consumer impl)     │
                               │   → RabbitMQ /       │
                               │     Kafka / SNS /    │
                               │     MediatR / vb.    │
                               └──────────────────────┘
```

---

## 3. Tam kurulum — Order servisi örneği

### 3.1 DbContext'e outbox tablosunu ekle

```csharp
using Microsoft.EntityFrameworkCore;
using NetCoreBackend.NArchitecture.Core.Outbox.EfPersistence;
using NetCoreBackend.NArchitecture.Core.Outbox.Entities;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Outbox tablosu + dispatch index'i (hot-path polling için filtered index)
        modelBuilder.ConfigureOutbox();

        // ... senin diğer entity konfigürasyonların
    }
}
```

Migration ekle:

```bash
dotnet ef migrations add AddOutbox
dotnet ef database update
```

### 3.2 RabbitMQ publisher'ı yaz

> Framework `IOutboxPublisher`'ı **boş bırakıyor** — çünkü her consumer farklı broker / topic / serializer kullanır. Sen kendi şirketinin konvansiyonuna uygun şekilde implement edersin.

```csharp
using System.Text;
using RabbitMQ.Client;
using NetCoreBackend.NArchitecture.Core.Outbox.Abstractions;
using NetCoreBackend.NArchitecture.Core.Outbox.Entities;

public sealed class RabbitMqOutboxPublisher : IOutboxPublisher, IAsyncDisposable
{
    private const string ExchangeName = "events";  // topic exchange
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqOutboxPublisher> _logger;

    public RabbitMqOutboxPublisher(IConnection connection, ILogger<RabbitMqOutboxPublisher> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        await using IChannel channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Routing key olarak EventType kullan — "Orders.Placed.v1" → topic.* binding'lerine
        // göre tüketici servislere dağıtılır.
        BasicProperties props = new()
        {
            MessageId     = message.Id.ToString(),
            CorrelationId = message.CorrelationId,
            ContentType   = "application/json",
            Type          = message.EventType,
            Timestamp     = new AmqpTimestamp(new DateTimeOffset(message.OccurredAtUtc).ToUnixTimeSeconds()),
            Persistent    = true  // disk-backed → broker restart'ında kaybolmaz
        };

        await channel.BasicPublishAsync(
            exchange:    ExchangeName,
            routingKey:  message.EventType,
            mandatory:   false,
            basicProperties: props,
            body:        Encoding.UTF8.GetBytes(message.Payload),
            cancellationToken: cancellationToken);

        // Burada throw atarsan OutboxPublisherWorker yakalar, AttemptCount++,
        // exponential backoff ile retry eder. Atmazsan MarkProcessedAsync çağrılır.
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
```

### 3.3 Program.cs — DI wiring

```csharp
using NetCoreBackend.NArchitecture.Core.Outbox.Abstractions;
using NetCoreBackend.NArchitecture.Core.Outbox.DependencyInjection;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// 1. DbContext (SQL Server / Postgres / vb.)
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("AppDb")));

// 2. Outbox store + worker
builder.Services.AddOutbox<AppDbContext>(opt =>
{
    opt.BatchSize      = 100;
    opt.MaxAttempts    = 8;
    opt.IdlePollDelay  = TimeSpan.FromSeconds(2);
    opt.BaseRetryDelay = TimeSpan.FromSeconds(2);
    opt.MaxRetryDelay  = TimeSpan.FromMinutes(10);
});

// 3. RabbitMQ connection — Singleton (connection pahalı, channel ucuz)
builder.Services.AddSingleton<IConnection>(sp =>
{
    var factory = new ConnectionFactory
    {
        Uri = new Uri(builder.Configuration.GetConnectionString("RabbitMq")!),
        AutomaticRecoveryEnabled = true,
        NetworkRecoveryInterval  = TimeSpan.FromSeconds(10)
    };
    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});

// 4. Senin publisher — Scoped (worker her batch için yeni scope açar)
builder.Services.AddScoped<IOutboxPublisher, RabbitMqOutboxPublisher>();

var app = builder.Build();
app.Run();
```

### 3.4 Handler içinde kullanım

```csharp
public sealed class PlaceOrderHandler : IRequestHandler<PlaceOrderCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly IOutboxStore _outbox;
    private readonly IHttpContextAccessor _http;

    public PlaceOrderHandler(AppDbContext db, IOutboxStore outbox, IHttpContextAccessor http)
    {
        _db = db;
        _outbox = outbox;
        _http = http;
    }

    public async Task<Guid> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        Order order = new()
        {
            Id         = Guid.NewGuid(),
            CustomerId = cmd.CustomerId,
            Total      = cmd.Total,
            CreatedAt  = DateTime.UtcNow
        };
        _db.Orders.Add(order);

        await _outbox.AppendAsync(new OutboxMessage
        {
            Id            = Guid.NewGuid(),
            EventType     = "Orders.Placed.v1",
            Payload       = JsonSerializer.Serialize(new
            {
                orderId    = order.Id,
                customerId = order.CustomerId,
                total      = order.Total
            }),
            CorrelationId = _http.HttpContext?.TraceIdentifier,
            OccurredAtUtc = DateTime.UtcNow
        }, ct);

        // ATOMIC: order + outbox row tek transaction'da. Crash olursa ikisi de gitmez.
        await _db.SaveChangesAsync(ct);

        return order.Id;
    }
}
```

> **Önemli:** `AppendAsync` çağrısı `SaveChanges` etmiyor — sadece DbContext'e ekliyor. Atomicity'i `SaveChangesAsync` sağlar. Eğer `TransactionScopeBehavior` (Core.Application pipeline) kullanıyorsan otomatik transaction da iş görür.

---

## 4. Components

| Type | Rol |
|---|---|
| `OutboxMessage` | `TenantEntity<Guid>` — `EventType`, `Payload`, `CorrelationId`, retry bookkeeping (`AttemptCount`, `NextAttemptUtc`, `IsPoisoned`, `Error`) |
| `IOutboxStore` | Storage soyutlaması — `AppendAsync`, `FetchDueAsync`, `MarkProcessedAsync`, `RecordFailureAsync` |
| `EfOutboxStore<TDbContext>` | EF Core implementasyonu — consumer DbContext üzerine binili, **default scoped impl** |
| `IOutboxPublisher` | **Consumer implementer** — gerçek event'i broker'a shipping eden tek interface |
| `OutboxPublisherWorker` | `BackgroundService` — polling + retry + poison-pill handling + per-message failure isolation |
| `OutboxOptions` | `BatchSize` / `IdlePollDelay` / `MaxAttempts` / `BaseRetryDelay` / `MaxRetryDelay` |
| `ConfigureOutbox()` | `ModelBuilder` extension — outbox tablosu + filtered dispatch index |

---

## 5. Retry policy

Exponential backoff: `attempt n → BaseRetryDelay × 2^(n-1)`, `MaxRetryDelay` ile cap'li.

Default ayarlarla (`BaseRetryDelay=2s`, `MaxAttempts=8`, `MaxRetryDelay=10m`) program:

| Attempt | Delay |
|---|---|
| 1 | 2s |
| 2 | 4s |
| 3 | 8s |
| 4 | 16s |
| 5 | 32s |
| 6 | 1m 4s |
| 7 | 2m 8s |
| 8 | 4m 16s |
| 9 | **POISONED** (`IsPoisoned = true`, dispatch durur, operator inceler) |

---

## 6. Failure isolation

`OutboxPublisherWorker` üç katman koruma kullanır:

1. **Per-message try/catch** — bir mesajın `PublishAsync`'te throw atması batch'in geri kalanını etkilemez; sadece o satırın retry counter'ı artar.
2. **Batch-level try/catch** — `FetchDueAsync` wholesale fail ederse (DB outage), worker log'lar ve `IdlePollDelay` kadar bekleyip yeniden dener. CPU spin yok.
3. **Cancellation respect** — `OperationCanceledException` host shutdown'ı temsil eder; mesaj penalize edilmez, sıradaki worker run'da yeniden fetch edilir.

---

## 7. Worker lifecycle

```
Host start
   ↓
ExecuteAsync loop:
   ├─ scope = scopeFactory.CreateAsyncScope()
   ├─ store     = scope.GetRequiredService<IOutboxStore>()
   ├─ publisher = scope.GetRequiredService<IOutboxPublisher>()
   ├─ due       = await store.FetchDueAsync(batchSize, ct)
   │
   ├─ if (due.Count == 0)
   │     await Task.Delay(IdlePollDelay)
   │     continue
   │
   ├─ foreach (msg in due)
   │     try { await publisher.PublishAsync(msg); store.MarkProcessed(msg); }
   │     catch { store.RecordFailure(msg, ...); }
   │
   └─ loop back immediately (drain backlog)
   ↓
Host stop → ExecuteAsync exits gracefully
```

Her batch için **yeni DI scope** açılır — DbContext per-batch tracker state'i biriktirmez, `IOutboxPublisher`'ın scoped dependency'leri (RabbitMQ channel gibi) düzgün dispose olur.

---

## 8. Sana ne zaman lazım?

| Senaryo | Outbox lazım mı? |
|---|---|
| Microservices arası event-driven iletişim (RabbitMQ / Kafka / SNS / Azure Service Bus) | ✅ **Kesinlikle** |
| Webhook gönderimi (Stripe-style: kendi consumer'larına notify) | ✅ Evet |
| Email/SMS notification — "user kaydını commit edince mail at" | ✅ Evet |
| Search index sync (PostgreSQL'e yaz → Elasticsearch'e index) | ✅ Evet |
| Audit log shipping (external SIEM'e) | ✅ Evet |
| Sadece monolith, dış sistem yok, in-process MediatR notification | ❌ Gereksiz — direkt `INotificationPublisher` yeterli |
| "Best-effort" yeterli, event kaybı tolere ediliyor | ⚠️ Belki — basit `_broker.PublishAsync` da iş görür |

---

## 9. Konfigürasyon

Tam appsettings + connection string örneği için bkz. **[SETUP.md → Outbox & RabbitMQ Configuration](../SETUP.md#outbox--rabbitmq-configuration)**.

---

## 10. İlgili dosyalar

- `Entities/OutboxMessage.cs`
- `Abstractions/IOutboxStore.cs`, `Abstractions/IOutboxPublisher.cs`
- `EfPersistence/EfOutboxStore.cs`, `EfPersistence/EfOutboxModelExtensions.cs`
- `Worker/OutboxPublisherWorker.cs`, `Worker/OutboxOptions.cs`
- `../Core.Outbox.DependencyInjection/OutboxServiceRegistration.cs`
