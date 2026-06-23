# Setup Guide

Setting up an ASP.NET Core project that uses NetCoreBackend (NArchitecture Core) from scratch.
This document is the **consuming app** side — the framework libraries are already written; you
only wire up the DI and middleware connections.

---

## 1. NuGet references

Which ones you use depends on your scenario, but a typical multi-tenant API pulls in the
following packages:

```xml
<ItemGroup>
  <!-- Core -->
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Application" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Persistence" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Persistence.DependencyInjection" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Persistence.WebApi" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Security" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Security.DependencyInjection" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.MultiTenancy" />

  <!-- Cross-cutting -->
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Abstraction" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Serilog" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Serilog.File" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.DependencyInjection" />

  <!-- Localization (YAML) -->
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Localization.Abstraction" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml.DependencyInjection" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Localization.WebApi" />

  <!-- Third-party runtimes (chosen by the consuming app) -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
  <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" />
  <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  <PackageReference Include="MediatR" />
</ItemGroup>
```

You can omit the ones you don't need (e.g. ElasticSearch, AmazonTranslate, MailKit).

---

## 2. `appsettings.json`

```jsonc
{
  "ConnectionStrings": {
    "AppDb":    "Server=localhost;Database=MyApp;Trusted_Connection=True;TrustServerCertificate=True",
    "Redis":    "localhost:6379",
    "RabbitMq": "amqp://guest:guest@localhost:5672/"      // see § 12 for production secrets
  },

  "TokenOptions": {
    "Audience": "myapp-clients",
    "Issuer": "myapp.com",
    "AccessTokenExpiration": 15,           // minutes
    "SecurityKey": "min-32-byte-utf8-secret-here-please-rotate",
    "RefreshTokenTtlDays": 7               // days — must match the property name exactly
  },

  "CacheSettings": {
    "SlidingExpirationDays": 7             // default used when ICachableRequest.SlidingExpiration does not override it
  },

  "FileLogConfiguration": {
    "FolderPath": "logs",                  // path traversal is rejected (`..`, no absolute paths)
    "MinLogLevel": "Information",
    "LogOutputTemplate": "[{Timestamp:dd.MM.yyyy HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
    "SpecificLogFolders": ["UserService", "OrderService"]
  },

  "MailSettings": {
    "Server": "smtp.example.com",
    "Port": 587,
    "SenderFullName": "MyApp",
    "SenderEmail": "noreply@myapp.com",
    "UserName": "smtp-user",
    "Password": "smtp-pass",
    "AuthenticationRequired": true,
    "TlsMode": "StartTlsWhenAvailable"     // None | Auto | SslOnConnect | StartTls | StartTlsWhenAvailable
  },

  "OutboxOptions": {
    "BatchSize": 100,                      // how many rows are processed per polling round
    "MaxAttempts": 8,                      // a row exceeding this becomes poisoned
    "IdlePollDelay": "00:00:02",           // wait time after an empty round
    "BaseRetryDelay": "00:00:02",          // exponential backoff base
    "MaxRetryDelay": "00:10:00"            // upper bound of the retry delay
  },

  "RabbitMqOptions": {
    "ExchangeName": "events",              // topic exchange
    "ExchangeType": "topic"
  },

  "EncryptionMasterKey": ""                // base64-encoded 32-byte AES-256 key — see § 12
}
```

> **`SecurityKey` must be at least 32 bytes UTF-8**; `TokenOptions.Validate()` checks this at
> startup. **`EncryptionMasterKey` is base64-encoded 32 bytes** (AES-256). Neither should live in
> appsettings in production — both must come from a secret store (§ 12).

---

## 3. `Program.cs` — Full Setup

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetCoreBackend.NArchitecture.Core.Application.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi.Extensions;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Abstraction;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Configurations;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Serilog.File;
using NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Localization.WebApi;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Persistence.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Persistence.WebApi;
using NetCoreBackend.NArchitecture.Core.Security.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Security.JWT;

var builder = WebApplication.CreateBuilder(args);

// ─── 1. Configuration objects ─────────────────────────────────────────────────
// AddSecurityServices() calls TokenOptions.Validate() at startup —
// if Audience, Issuer, SecurityKey (≥32 bytes), AccessTokenExpiration, or RefreshTokenTtlDays
// is missing, the application will not start (avoids the silent 0 / IDX10720 confusion).
var tokenOptions = builder.Configuration
    .GetSection("TokenOptions").Get<TokenOptions>()
    ?? throw new InvalidOperationException("TokenOptions missing.");

var fileLogConfig = builder.Configuration
    .GetSection("FileLogConfiguration").Get<FileLogConfiguration>()
    ?? throw new InvalidOperationException("FileLogConfiguration missing.");

// ─── 2. EF Core DbContext ──────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ─── 3. Multi-tenancy (ITenantContext, ITenantEntitySetter, middleware) ──────
builder.Services.AddMultiTenancy();
builder.Services.AddScoped<ITenantService, MyTenantService>();   // you implement this

// ─── 4. Security (ITokenHelper, password & authenticator helpers) ────────────
builder.Services.AddSecurityServices<Guid, Guid, Guid>(tokenOptions);
//                                    ^TUserId ^TOperationClaimId ^TRefreshTokenId
//   change these to match your application's entity Id types

// ─── 5. JWT Bearer authentication ─────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = tokenOptions.Issuer,
            ValidAudience = tokenOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SecurityKeyHelper.CreateSecurityKey(tokenOptions.SecurityKey),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// ─── 6. MediatR + all pipeline behaviors in one call ─────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddNArchitecturePipelineBehaviors();    // ← Authorization, Caching, Logging, ...

// ─── 7. Pipeline behavior prerequisites ──────────────────────────────────────
// NOTE: AddNArchitecturePipelineBehaviors() now registers IHttpContextAccessor itself
// via TryAddSingleton — the manual call is optional (idempotent).
// builder.Services.AddHttpContextAccessor();                                       // (optional) Auth, Tenant, Caching, Logging
builder.Services.AddStackExchangeRedisCache(o =>                                    // CachingBehavior, CacheRemovingBehavior
    o.Configuration = builder.Configuration.GetConnectionString("Redis"));
// Alternative (dev/local): builder.Services.AddDistributedMemoryCache();

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);               // RequestValidationBehavior

// ─── 8. Logging (ILogger from Core.CrossCuttingConcerns.Logging.Abstraction) ─
builder.Services.AddLogging(new SerilogFileLogger(fileLogConfig));                  // LoggingBehavior, ExceptionMiddleware

// ─── 9. Localization (YAML) ───────────────────────────────────────────────────
builder.Services.AddYamlResourceLocalization();    // ILocalizationService → ResourceLocalizationManager

// ─── 10. EF migration applier (auto-migrates at startup) ─────────────────────
builder.Services.AddDbMigrationApplier<AppDbContext>();

// ─── 11. Controllers + Swagger ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ─── Middleware order is CRITICAL ────────────────────────────────────────────
app.ConfigureCustomExceptionMiddleware();   // First in line — catches exceptions from the middleware that follow

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();

app.UseAuthentication();        // JWT parsing — BEFORE TenantMiddleware
app.UseMultiTenancy();          // tenant_id from JWT, header, subdomain
app.UseResponseLocalization();  // Accept-Language → ILocalizationService.AcceptLocales
app.UseAuthorization();

app.MapControllers();

// Apply migrations automatically (Database.Migrate)
app.UseDbMigrationApplier();

app.Run();
```

### Why is the middleware order like this?

```
ConfigureCustomExceptionMiddleware  ← catches exceptions from everything that comes after
UseAuthentication                   ← JWT parsing happens here, User.Claims is populated
UseMultiTenancy                     ← reads tenant_id from the JWT claim (priority 1)
UseResponseLocalization             ← after MultiTenancy, for the TenantContext.DefaultLocale fallback
UseAuthorization                    ← SuperAdmin / role checks
```

If the order is reversed:
- If `UseMultiTenancy` comes first, the JWT has not been parsed yet, so `User.Claims` is empty and
  the tenant is resolved from the wrong sources.
- If `UseResponseLocalization` comes first, `tenantContext.DefaultLocale` is still null, so the
  fallback does not work.

---

## 4. DbContext — Tenant filter and the PlatformAdmin table

```csharp
public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public DbSet<Product> Products => Set<Product>();
    public DbSet<User<Guid>> Users => Set<User<Guid>>();
    public DbSet<RefreshToken<Guid, Guid>> RefreshTokens => Set<RefreshToken<Guid, Guid>>();
    public DbSet<UserOperationClaim<Guid, Guid, Guid>> UserOperationClaims => Set<UserOperationClaim<Guid, Guid, Guid>>();
    public DbSet<OperationClaim<Guid>> OperationClaims => Set<OperationClaim<Guid>>();   // tenant-wide
    public DbSet<PlatformAdmin<Guid>> PlatformAdmins => Set<PlatformAdmin<Guid>>();      // separate table
    public DbSet<Tenant> Tenants => Set<Tenant>();                                       // platform-wide

    public AppDbContext(DbContextOptions<AppDbContext> opt, ITenantContext tenantContext)
        : base(opt)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Tenant.Identifier UNIQUE — for TenantMiddleware GetBySlugAsync
        builder.Entity<Tenant>().HasIndex(t => t.Identifier).IsUnique();
        builder.Entity<Tenant>()
            .HasIndex(t => t.Domain).IsUnique()
            .HasFilter("[Domain] IS NOT NULL");

        // PlatformAdmin email global unique
        builder.Entity<PlatformAdmin<Guid>>().HasIndex(a => a.Email).IsUnique();

        // Tenant user email — (TenantId, Email) composite unique
        builder.Entity<User<Guid>>().HasIndex(u => new { u.TenantId, u.Email }).IsUnique();

        // EF Core global query filter — on every ITenantEntity
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType)) continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProp = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
            var ctxConst = Expression.Constant(_tenantContext);
            var ctxTenantId = Expression.Property(
                Expression.Property(ctxConst, nameof(ITenantContext.TenantId)),
                "Value");
            var isSuperAdmin = Expression.Property(ctxConst, nameof(ITenantContext.IsSuperAdmin));

            // SuperAdmin all rows; tenant user only their own
            var filter = Expression.OrElse(isSuperAdmin, Expression.Equal(tenantIdProp, ctxTenantId));
            builder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(filter, parameter));
        }

        base.OnModelCreating(builder);
    }
}
```

---

## 5. `ITenantService` implementation

```csharp
public class MyTenantService : ITenantService
{
    private readonly AppDbContext _ctx;
    public MyTenantService(AppDbContext ctx) => _ctx = ctx;

    // IgnoreQueryFilters() — the tenant lookup must not pass through its own filter
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _ctx.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => _ctx.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Identifier == slug, ct);

    public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default)
        => _ctx.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Domain == domain, ct);
}
```

---

## 6. Pipeline Behavior — Marker Interface Reference

`AddNArchitecturePipelineBehaviors()` registers 9 behaviors; each is opt-in.

| Behavior | Trigger interface | What it does |
|---|---|---|
| `AuthorizationBehavior` | `ISecuredRequest` | Role check against the `Roles` array. A PlatformAdmin (`is_super_admin=true`) skips all role checks; if `Roles=["SuperAdmin"]` is required, only a PlatformAdmin passes. |
| `SuperAdminBlockBehavior` | `IBlockedForSuperAdmin` | Prevents a PlatformAdmin (without impersonating) from invoking tenant-user-only operations. |
| `TenantValidationBehavior` | `ITenantValidationRequest` | `AuthorizationException` if there is no active tenant context. SuperAdmin is exempt. |
| `RequestValidationBehavior` | automatic (always) | Runs if a FluentValidation `IValidator<TRequest>` exists, `ValidationException` → 400. |
| `CachingBehavior` | `ICachableRequest` | Distributed cache lookup → handler on miss → cache write. Key is tenant-prefixed. |
| `CacheRemovingBehavior` | `ICacheRemoverRequest` | Cache invalidation via `CacheGroupKey` or `CacheKey`. |
| `LoggingBehavior` | `ILoggableRequest` | Logs request body + user + tenant_id. If `ISensitiveRequest` is also implemented, the payload is `[redacted]`. |
| `TransactionScopeBehavior` | `ITransactionalRequest` | Rolls back the `TransactionScope` if the handler throws. |
| `PerformanceBehavior` | `IIntervalRequest` | Warning log if `Interval` (seconds) is exceeded. |

---

## 7. Example Handler — multiple behaviors active

```csharp
public record GetProductsQuery(int PageIndex = 0, int PageSize = 10)
    : IRequest<GetListResponse<ProductDto>>,
      ISecuredRequest,                     // AuthorizationBehavior
      ITenantValidationRequest,            // TenantValidationBehavior
      ICachableRequest,                    // CachingBehavior
      ILoggableRequest                     // LoggingBehavior
{
    public string[] Roles => ["Manager", "Admin"];
    public string CacheKey => $"Products:GetAll:{PageIndex}:{PageSize}";
    public string? CacheGroupKey => "Products";       // cleared on any Product mutation
    public bool BypassCache => false;
    public TimeSpan? SlidingExpiration => null;       // use the CacheSettings default
}

public class CreateProductCommand : IRequest<Guid>,
    ISecuredRequest,
    ITenantValidationRequest,
    ITransactionalRequest,
    ICacheRemoverRequest                    // CacheRemovingBehavior
{
    public required string Name { get; init; }
    public required decimal Price { get; init; }

    public string[] Roles => ["Admin"];
    public bool BypassCache => false;
    public string? CacheKey => null;        // we are only clearing the group
    public string[]? CacheGroupKey => ["Products"];
}

// Command whose payload is sensitive
public record LoginCommand(string Email, string Password)
    : IRequest<LoginResponse>,
      ISensitiveRequest                     // LoggingBehavior turns the body into "[redacted]"
{ }
```

---

## 8. Cherry-pick — when only some behaviors are needed

**Do not call** the `AddNArchitecturePipelineBehaviors()` umbrella; register only the ones you want:

```csharp
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
// LoggingBehavior, TransactionScopeBehavior, etc. are not registered → bypassed
```

Performance cost: the overhead of using the umbrella registration is negligible (behaviors run
opt-in, and if a request does not implement the marker, the pipeline does not invoke that behavior).
Cherry-picking is only necessary when the consuming app wants full control over its MediatR
pipeline.

---

## 9. Migration

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

`UseDbMigrationApplier()` automatically applies pending migrations at startup. In production it is
safer to run migrations separately through your CI/CD pipeline; this extension is a convenience for
dev/test.

---

## 10. Auth flow — quick summary

Details: [AUTH.md](./AUTH.md) and [TENANT.md](./TENANT.md)

```
POST /api/auth/login                ← email + password (+ X-Tenant-ID for users)
   ↓
   In the PlatformAdmin table? → CreateAdminToken (is_super_admin: true)
   ↓ no
   In the User table? (EF filter scoped to tenant)
      ↓ yes → password verify → CreateToken (with tenant_id claim)

POST /api/auth/refresh              ← refresh token + X-Tenant-ID
POST /api/auth/impersonate          ← SuperAdmin token + tenantId → impersonation token
POST /api/auth/impersonate/exit     ← impersonation/SuperAdmin token → plain SuperAdmin token
```

---

## 11. Quick checklist — before going to production

- [ ] `TokenOptions.SecurityKey` is ≥ 32 bytes UTF-8 and comes from a KMS/secret store (NOT in appsettings)
- [ ] `TokenOptions.RefreshTokenTtlDays` is set (not 0 — otherwise the refresh token expires immediately)
- [ ] `EncryptionMasterKey` is 32-byte base64 and comes from a secret store (for the TOTP secret, recovery codes, etc.)
- [ ] The Redis connection works (in-memory cache is not suitable for production)
- [ ] The RabbitMQ/Kafka connection string comes from a secret store (`amqp://user:pass@...` MUST NOT sit in plain config)
- [ ] There is a unique index on `Tenant.Identifier` and `Domain`
- [ ] There is a `(TenantId, Email)` composite unique index on `User.Email`
- [ ] `OtpAuthenticator.SecretKey` is stored encrypted with `AesGcmEncryptionHelper`
- [ ] `ConfigureCustomExceptionMiddleware` is first in the middleware order
- [ ] `UseAuthentication` comes before `UseMultiTenancy`
- [ ] The login handler checks `IsLegacyHash` and lazily migrates to PBKDF2 (if migrating from a legacy system)
- [ ] CI/CD migration runs as a separate step (UseDbMigrationApplier is for dev/test only)
- [ ] Validation errors (`PageRequest [Range]`, etc.) are caught via a ModelState check in the controllers
- [ ] Sensitive commands implement `ISensitiveRequest`
- [ ] If Outbox is used, the migration for the `OutboxMessages` table has been applied (`ConfigureOutbox()` is called in the model)
- [ ] The `IOutboxPublisher` implementation is registered and the broker connection is healthy
- [ ] There is an alerting / dashboard query for Outbox poison rows (`SELECT * FROM OutboxMessages WHERE IsPoisoned = 1`)

---

## 12. Outbox & RabbitMQ Configuration

> **Why is Outbox needed?** Detailed problem/solution: [Core.Outbox/README.md](./Core.Outbox/README.md).
> This section only covers the config + secret management side.

### 12.1 Connection strings

They sit in `appsettings.json` as a **placeholder**; the **production value comes from a SECRET STORE**:

```jsonc
"ConnectionStrings": {
  "AppDb":    "...",                              // local SQL for dev
  "RabbitMq": "amqp://guest:guest@localhost:5672/"  // local broker for dev
}
```

### 12.2 Dev / Test → User-secrets

Each developer may have a broker installed on their machine, with different credentials. **User-secrets** injects local values without polluting appsettings:

```bash
cd MyApp.WebApi
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:RabbitMq" "amqp://caner:dev-pass@localhost:5672/"
dotnet user-secrets set "ConnectionStrings:AppDb"    "Server=localhost;Database=MyApp_Caner;..."
dotnet user-secrets set "EncryptionMasterKey"        "Y2gxdHF1aWNrLWJhc2U2NC0zMi1ieXRlcy1mb3ItZGV2"
dotnet user-secrets set "TokenOptions:SecurityKey"   "dev-only-32-byte-secret-rotate-me-pls"
```

`.NET Generic Host` reads user-secrets automatically in the `Development` environment — it overrides the placeholders in `appsettings.json`.

### 12.3 Production → Secret manager

| Platform | Secret store | Bind command |
|---|---|---|
| Azure | Key Vault | `builder.Configuration.AddAzureKeyVault(...)` |
| AWS | Secrets Manager / Parameter Store | `builder.Configuration.AddSystemsManager(...)` |
| Kubernetes | Sealed Secrets / External Secrets Operator | env var → `__` separator (`ConnectionStrings__RabbitMq`) |
| Docker Compose | `secrets:` section + file mount | `/run/secrets/rabbitmq` → custom provider |

**Minimum rule:** the following keys must not sit in plain text in appsettings.json — they all come from a secret store:

- `ConnectionStrings:AppDb`
- `ConnectionStrings:RabbitMq` (or Kafka bootstrap servers + SASL credentials)
- `ConnectionStrings:Redis` (if auth is enabled)
- `TokenOptions:SecurityKey`
- `EncryptionMasterKey`
- `MailSettings:Password`

### 12.4 Generating the EncryptionMasterKey

`AesGcmEncryptionHelper.GenerateKey()` produces 32 bytes. Base64-encode it and write it to the secret store:

```csharp
// One-off script or REPL:
var key = AesGcmEncryptionHelper.GenerateKey();
Console.WriteLine(Convert.ToBase64String(key));
// → "M3kP...44 char base64 string..." (32 bytes = ~44 chars)
```

If the `EncryptionMasterKey` needs rotation, prefix a version byte to the blob layout; on decrypt, select the key based on the prefix (details: top-of-file comment in `Core.Security/Encryption/AesGcmEncryptionHelper.cs`).

### 12.5 Program.cs — DI

```csharp
using NetCoreBackend.NArchitecture.Core.Outbox.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Security.Encryption;
using RabbitMQ.Client;

// 1. DbContext
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("AppDb")));

// 2. Multi-tenancy — REQUIRED if the outbox is multi-tenant SaaS. Must come before AddOutbox;
// EfOutboxStore.AppendAsync resolves ITenantEntitySetter to stamp the TenantId.
// (Single-tenant apps can skip this; then the handlers set msg.TenantId explicitly.)
builder.Services.AddMultiTenancy();

// 3. Outbox — bind options from appsettings with Configure, then AddOutbox store + worker.
// AddOutbox calls OutboxOptions.Validate() via ValidateOnStart();
// misconfiguration such as BatchSize=0 or MaxRetryDelay<BaseRetryDelay fails the host build.
builder.Services.Configure<OutboxOptions>(
    builder.Configuration.GetSection("OutboxOptions"));
builder.Services.AddOutbox<AppDbContext>();               // store + worker

// 4. RabbitMQ connection — Singleton (connection is expensive, channel is cheap)
builder.Services.AddSingleton<IConnection>(sp =>
{
    var factory = new ConnectionFactory
    {
        Uri = new Uri(builder.Configuration.GetConnectionString("RabbitMq")!),
        AutomaticRecoveryEnabled = true,
        NetworkRecoveryInterval  = TimeSpan.FromSeconds(10),
        ClientProvidedName       = "myapp-publisher"
    };
    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});

// 5. Your IOutboxPublisher implementation
builder.Services.AddScoped<IOutboxPublisher, RabbitMqOutboxPublisher>();

// 6. AES-GCM master key — load once at startup, inject as a Singleton.
// The EncryptionMasterKey wrapper validates the 32-byte length in its ctor; it also
// prevents bare `byte[]` Singletons from being confused with each other in the DI graph.
byte[] masterKey = Convert.FromBase64String(
    builder.Configuration["EncryptionMasterKey"]
    ?? throw new InvalidOperationException("EncryptionMasterKey missing."));
builder.Services.AddSingleton(new EncryptionMasterKey(masterKey));
```

### 12.6 Migration

Create the `OutboxMessages` table:

```bash
dotnet ef migrations add AddOutbox
dotnet ef database update
```

The `ConfigureOutbox()` extension method must have been called in `OnModelCreating` (example: [Core.Outbox/README.md § 3.1](./Core.Outbox/README.md#31-dbcontexte-outbox-tablosunu-ekle)).

### 12.7 Poison message monitoring

Outbox poison rows must not pile up silently — set up alerting:

```sql
-- Operator dashboard query
SELECT TOP 100 Id, EventType, AttemptCount, Error, OccurredAtUtc
FROM OutboxMessages
WHERE IsPoisoned = 1
ORDER BY OccurredAtUtc DESC;
```

When a poison row is detected:
1. Find the root cause (broker down? schema mismatch? consumer bug?).
2. Fix it and **manually reset**: `UPDATE OutboxMessages SET IsPoisoned = 0, AttemptCount = 0, NextAttemptUtc = NULL WHERE Id = '...'`.
3. The worker picks it up on the next polling round and retries.

If the event is no longer needed, **archive** it with `ProcessedAtUtc = GETUTCDATE()` rather than deleting it.
