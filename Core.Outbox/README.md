# Core.Outbox

Implementation of the **Transactional Outbox** pattern. Locks the atomic DB write + event publish into a single transaction. For sending distributed events, it solves the question "what happens if a crash occurs between publish and DB commit?".

> **TL;DR:** If you send events to external systems such as RabbitMQ / Kafka / SNS, the "DB commit OK but event publish FAIL" scenario puts your system into a permanently inconsistent state. The Outbox makes this scenario impossible.

---

## 1. The problem it solves

### ❌ The classic broken pattern

```csharp
public async Task PlaceOrderAsync(Order order, CancellationToken ct)
{
    _db.Orders.Add(order);
    await _db.SaveChangesAsync(ct);              // 1. DB commit OK

    // ⚠️ If a crash occurs on this line (network glitch, container OOM kill, process restart):
    //    - The order is in the DB
    //    - The event was NOT sent to RabbitMQ
    //    - The Inventory service never receives the "OrderPlaced" notification
    //    - The system is in a permanently inconsistent state → manual reconciliation is required
    await _rabbit.PublishAsync(new OrderPlacedEvent(order.Id), ct);
}
```

If you try the reverse (publish first, then DB), this time:
- Publish OK but DB commit fails → consumers react to a nonexistent order → a storm of `OrderNotFound` exceptions

Trying to fix it with `try/catch + retry` is not a solution either: if the process dies entirely, even the retry logic won't run.

### ✅ The Outbox solution

```csharp
public async Task PlaceOrderAsync(Order order, CancellationToken ct)
{
    _db.Orders.Add(order);

    // The outbox row is ALSO added to the same DbContext → a single transaction
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

Then, in the background, `OutboxPublisherWorker` polls the outbox table and delivers it to the `IOutboxPublisher` written by the consumer. Three scenarios:

| Scenario | Result |
|---|---|
| Crash before DB commit | Neither is written → the user gets a 500, retries, no inconsistency |
| Commit OK, then crash | The outbox row is persisted → when the worker restarts it picks it up, sends it to RabbitMQ, and stamps `ProcessedAtUtc` |
| RabbitMQ down | The worker fails, `AttemptCount++`, retries with exponential backoff, and if `MaxAttempts` is exceeded `IsPoisoned = true` → an operator investigates. **No event loss.** |

---

## 2. Flow diagram

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
                               │     MediatR / etc.   │
                               └──────────────────────┘
```

---

## 3. Full setup — Order service example

### 3.1 Add the outbox table to the DbContext

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

        // Outbox table + dispatch index (a filtered index for hot-path polling)
        modelBuilder.ConfigureOutbox();

        // ... your other entity configurations
    }
}
```

Add a migration:

```bash
dotnet ef migrations add AddOutbox
dotnet ef database update
```

### 3.2 Write the RabbitMQ publisher

> The framework **leaves `IOutboxPublisher` empty** — because every consumer uses a different broker / topic / serializer. You implement it according to your own company's conventions.

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

        // Use EventType as the routing key — "Orders.Placed.v1" → distributed to consumer
        // services according to topic.* bindings.
        BasicProperties props = new()
        {
            MessageId     = message.Id.ToString(),
            CorrelationId = message.CorrelationId,
            ContentType   = "application/json",
            Type          = message.EventType,
            Timestamp     = new AmqpTimestamp(new DateTimeOffset(message.OccurredAtUtc).ToUnixTimeSeconds()),
            Persistent    = true  // disk-backed → not lost on a broker restart
        };

        await channel.BasicPublishAsync(
            exchange:    ExchangeName,
            routingKey:  message.EventType,
            mandatory:   false,
            basicProperties: props,
            body:        Encoding.UTF8.GetBytes(message.Payload),
            cancellationToken: cancellationToken);

        // If you throw here, OutboxPublisherWorker catches it, AttemptCount++,
        // and retries with exponential backoff. If you don't throw, MarkProcessedAsync is called.
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

// 1. DbContext (SQL Server / Postgres / etc.)
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("AppDb")));

// 2. Multi-tenancy — REQUIRED in a multi-tenant SaaS scenario. Must come before AddOutbox;
// EfOutboxStore.AppendAsync resolves ITenantEntitySetter to stamp the TenantId.
// If there is no setter + msg.TenantId is empty, append throws a loud error (no orphan row).
builder.Services.AddMultiTenancy();

// 3. Outbox store + worker.
// OutboxOptions calls Validate() at startup via ValidateOnStart() —
// misconfigurations such as BatchSize=0 / MaxRetryDelay<BaseRetryDelay fail the host build.
builder.Services.AddOutbox<AppDbContext>(opt =>
{
    opt.BatchSize      = 100;
    opt.MaxAttempts    = 8;
    opt.IdlePollDelay  = TimeSpan.FromSeconds(2);
    opt.BaseRetryDelay = TimeSpan.FromSeconds(2);
    opt.MaxRetryDelay  = TimeSpan.FromMinutes(10);
});

// 4. RabbitMQ connection — Singleton (connection is expensive, channel is cheap)
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

// 5. Your publisher — Scoped (the worker opens a new scope for each batch)
builder.Services.AddScoped<IOutboxPublisher, RabbitMqOutboxPublisher>();

var app = builder.Build();
app.Run();
```

### 3.4 Usage inside the handler

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

        // ATOMIC: order + outbox row in a single transaction. If a crash occurs, neither is committed.
        await _db.SaveChangesAsync(ct);

        return order.Id;
    }
}
```

> **Important:** The `AppendAsync` call does not `SaveChanges` — it only adds to the DbContext. `SaveChangesAsync` provides the atomicity. If you use `TransactionScopeBehavior` (the Core.Application pipeline), the automatic transaction also does the job.

---

## 4. Components

| Type | Role |
|---|---|
| `OutboxMessage` | `TenantEntity<Guid>` — `EventType`, `Payload`, `CorrelationId`, retry bookkeeping (`AttemptCount`, `NextAttemptUtc`, `IsPoisoned`, `Error`) |
| `IOutboxStore` | Storage abstraction — `AppendAsync`, `FetchDueAsync`, `MarkProcessedAsync`, `RecordFailureAsync` |
| `EfOutboxStore<TDbContext>` | EF Core implementation — bound to the consumer DbContext, **default scoped impl** |
| `IOutboxPublisher` | **Consumer implementer** — the only interface that ships the actual event to the broker |
| `OutboxPublisherWorker` | `BackgroundService` — polling + retry + poison-pill handling + per-message failure isolation |
| `OutboxOptions` | `BatchSize` / `IdlePollDelay` / `MaxAttempts` / `BaseRetryDelay` / `MaxRetryDelay` |
| `ConfigureOutbox()` | `ModelBuilder` extension — outbox table + filtered dispatch index |

---

## 5. Retry policy

Exponential backoff: `attempt n → BaseRetryDelay × 2^(n-1)`, capped by `MaxRetryDelay`.

With the default settings (`BaseRetryDelay=2s`, `MaxAttempts=8`, `MaxRetryDelay=10m`) the schedule is:

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
| 9 | **POISONED** (`IsPoisoned = true`, dispatch stops, an operator investigates) |

---

## 6. Failure isolation

`OutboxPublisherWorker` uses three layers of protection:

1. **Per-message try/catch** — a message throwing in `PublishAsync` does not affect the rest of the batch; only that row's retry counter is incremented.
2. **Batch-level try/catch** — if `FetchDueAsync` fails wholesale (DB outage), the worker logs, waits for `IdlePollDelay`, and retries. No CPU spin.
3. **Cancellation respect** — `OperationCanceledException` represents host shutdown; the message is not penalized and is re-fetched on the next worker run.

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

A **new DI scope** is opened for each batch — the DbContext does not accumulate per-batch tracker state, and the `IOutboxPublisher`'s scoped dependencies (such as the RabbitMQ channel) are disposed properly.

---

## 7.1 Horizontal scaling — known limitation

`OutboxPublisherWorker` is designed to run as a **single replica**. If multiple replicas call `FetchDueAsync` at the same time, they may publish **the same row twice** (a pessimistic lock / `FOR UPDATE SKIP LOCKED` is not in the framework — it is provider-specific).

### Practical solutions

| Approach | Description |
|---|---|
| **Single replica** (recommended) | K8s `Deployment.replicas: 1` or a single BackgroundService host. The worker is already cheap (polling-based); for throughput, **increasing `BatchSize`** usually yields better results than scaling up replicas. |
| **Leader election** | If there are multiple hosts, allow only the "leader" worker to run (ZooKeeper / Consul / Redis lock). Add an `IHostedService` condition to the worker so that an instance that cannot acquire the lock exits immediately. |
| **Custom store + FOR UPDATE SKIP LOCKED** | Replace `IOutboxStore` with your own raw-SQL implementation; for Postgres/MySQL/SQL Server, `SELECT … FOR UPDATE SKIP LOCKED` enables safe consumption by multiple workers. |
| **Idempotent consumer** | In a scenario where the publisher does not throw but the message is delivered twice, if the consumer side already has an idempotency key check, a duplicate publish is harmless data-wise. |

> The default setup (single replica) is sufficient for most SaaS — the outbox workload is typically on the order of hundreds of messages per second, which batch=100 handles comfortably.

---

## 7.2 Multi-tenant semantics

The Outbox is a `TenantEntity` — every row is bound to a `TenantId`, but the two paths behave **differently**:

### Write path (handler → AppendAsync)

If `TenantId == Guid.Empty`, `AppendAsync` stamps it from the current `ITenantEntitySetter`. Typical flow:

```
Request → TenantMiddleware → TenantContext.SetTenant(tenantA)
   → PlaceOrderHandler → _outbox.AppendAsync(msg)   // msg.TenantId empty
   → EfOutboxStore stamps it: msg.TenantId = tenantA
   → SaveChangesAsync atomic commit
```

If there is no tenant context + `ITenantEntitySetter` is not registered, a **loud error** is thrown — an orphan row is never written.

### Read path (worker → FetchDueAsync)

`OutboxPublisherWorker` is a `BackgroundService`. There is no HttpContext, TenantMiddleware has not run, and the tenant context is empty. **The worker is cross-tenant by design** — it must drain the outbox of all tenants, otherwise no events are shipped at all.

That is why `FetchDueAsync` calls `IgnoreQueryFilters()`. Even if the consumer's `DbContext` has a tenant query filter on `OutboxMessage`, the worker sees all rows.

> **If you want per-tenant routing**, you can read `message.TenantId` inside `IOutboxPublisher.PublishAsync` and route to a tenant-specific topic/queue — even if TenantId is not written to the payload, it is present on the entity.

### Contract summary

| Path | Tenant context required? | Filter behavior |
|---|---|---|
| `AppendAsync` | Yes — stamp from TenantSetter or set `msg.TenantId` explicitly | — |
| `FetchDueAsync` | No — the worker sees all tenants | `IgnoreQueryFilters()` |
| `MarkProcessedAsync` / `RecordFailureAsync` | No — the entity is already in the tracker, just SaveChanges | — |

---

## 7.3 DbContext isolation — known limitation

`EfOutboxStore.MarkProcessedAsync` and `RecordFailureAsync` call `SaveChangesAsync` — this commits **ALL tracked changes in the DbContext**, not just the outbox row.

- **The worker path is safe**: `OutboxPublisherWorker` opens a fresh DI scope for each batch → the DbContext keeps only outbox rows in the tracker. ✅
- **The consumer must be careful**: if you use `IOutboxStore` from another handler over a request-scoped DbContext, `SaveChangesAsync` may also commit other entities tracked in the request.

**Practical recommendation:** Use `IOutboxStore` only for `AppendAsync`; leave `MarkProcessed/RecordFailure` to the worker. `AppendAsync` does not call `SaveChangesAsync` anyway — it only Adds to the DbContext, and the caller's transaction provides the atomicity.

---

## 8. When do you need it?

| Scenario | Is the Outbox needed? |
|---|---|
| Event-driven communication between microservices (RabbitMQ / Kafka / SNS / Azure Service Bus) | ✅ **Absolutely** |
| Sending webhooks (Stripe-style: notify your own consumers) | ✅ Yes |
| Email/SMS notification — "send mail when the user registration is committed" | ✅ Yes |
| Search index sync (write to PostgreSQL → index into Elasticsearch) | ✅ Yes |
| Audit log shipping (to an external SIEM) | ✅ Yes |
| Monolith only, no external system, in-process MediatR notification | ❌ Unnecessary — `INotificationPublisher` directly is enough |
| "Best-effort" is sufficient, event loss is tolerated | ⚠️ Maybe — a simple `_broker.PublishAsync` will also do the job |

---

## 9. Configuration

For a full appsettings + connection string example, see **[SETUP.md → Outbox & RabbitMQ Configuration](../SETUP.md#12-outbox--rabbitmq-configuration)**.

---

## 10. Related files

- `Entities/OutboxMessage.cs`
- `Abstractions/IOutboxStore.cs`, `Abstractions/IOutboxPublisher.cs`
- `EfPersistence/EfOutboxStore.cs`, `EfPersistence/EfOutboxModelExtensions.cs`
- `Worker/OutboxPublisherWorker.cs`, `Worker/OutboxOptions.cs`
- `../Core.Outbox.DependencyInjection/OutboxServiceRegistration.cs`
