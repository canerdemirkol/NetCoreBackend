# NetCoreBackend — NArchitecture Core

**NetCoreBackend** is a production-ready, modular backend infrastructure library built on **.NET 10**, designed for Clean Architecture and SaaS (multi-tenant) applications. It provides all the cross-cutting concerns, persistence abstractions, security primitives, and application pipeline behaviors needed to build enterprise-grade APIs.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Your Application                         │
│         (Web API / MVC / Worker / gRPC)                     │
└──────┬──────────────────────────────────────────────────────┘
       │ references
       ▼
┌─────────────────────────────────────────────────────────────┐
│                   Core Layer (This Repo)                    │
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────────┐ │
│  │Core.Application│ │Core.Security │  │Core.MultiTenancy  │ │
│  │  MediatR      │ │  JWT / OTP   │  │  Tenant Middleware │ │
│  │  Pipelines    │ │  Hashing     │  │  ITenantContext    │ │
│  └──────────────┘  └──────────────┘  └───────────────────┘ │
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────────┐ │
│  │Core.Persistence│ │Core.Logging  │  │Core.Localization  │ │
│  │  EF Core Base  │ │  Serilog     │  │  YAML Resources   │ │
│  │  Repository    │ │  Abstraction │  │  Translation      │ │
│  └──────────────┘  └──────────────┘  └───────────────────┘ │
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────────┐ │
│  │Core.Mailing   │ │Core.ElasticSearch│ │Core.Translation  │ │
│  │  MailKit      │ │  NEST client  │  │  Amazon Translate │ │
│  └──────────────┘  └──────────────┘  └───────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

## Class Libraries

| Project | Purpose |
|---|---|
| [`Core.Application`](Core.Application/README.md) | MediatR pipelines, CQRS base, DTOs, business rules |
| [`Core.Persistence`](Core.Persistence/README.md) | EF Core repository pattern, paging, dynamic query, tenant entity base |
| [`Core.Security`](Core.Security/README.md) | JWT, refresh tokens, PBKDF2 hashing, OTP, claim extensions, AES encryption |
| [`Core.Security.DependencyInjection`](Core.Security.DependencyInjection/README.md) | DI registration with startup `TokenOptions.Validate()` |
| [`Core.Security.WebApi.Swagger`](Core.Security.WebApi.Swagger/README.md) | `AddBearerSecurity()` one-call Swagger Bearer scheme + filter |
| [`Core.MultiTenancy`](Core.MultiTenancy/README.md) | Multi-tenant middleware, ITenantContext, tenant resolution |
| [`Core.CrossCuttingConcerns.Exception`](Core.CrossCuttingConcerns.Exception/README.md) | Domain exception types |
| [`Core.CrossCuttingConcerns.Exception.WebAPI`](Core.CrossCuttingConcerns.Exception.WebAPI/README.md) | Global exception handler for Web API |
| [`Core.CrossCuttingConcerns.Logging`](Core.CrossCuttingConcerns.Logging/README.md) | LogDetail, LogParameter models |
| [`Core.CrossCuttingConcerns.Logging.Abstraction`](Core.CrossCuttingConcerns.Logging.Abstraction/README.md) | ILogger abstraction |
| [`Core.CrossCuttingConcerns.Logging.SeriLog`](Core.CrossCuttingConcerns.Logging.SeriLog/README.md) | Serilog implementation |
| [`Core.CrossCuttingConcerns.Logging.Serilog.File`](Core.CrossCuttingConcerns.Logging.Serilog.File/README.md) | File sink configuration |
| [`Core.CrossCuttingConcerns.Logging.DependencyInjection`](Core.CrossCuttingConcerns.Logging.DependencyInjection/README.md) | DI registration for logging |
| [`Core.Localization.Abstraction`](Core.Localization.Abstraction/README.md) | ILocalizationService abstraction |
| [`Core.Localization.Resource.Yaml`](Core.Localization.Resource.Yaml/README.md) | YAML-based resource files |
| [`Core.Localization.Resource.Yaml.DependencyInjection`](Core.Localization.Resource.Yaml.DependencyInjection/README.md) | DI registration for YAML localization |
| [`Core.Localization.Translation`](Core.Localization.Translation/README.md) | Translation pipeline |
| [`Core.Localization.WebApi`](Core.Localization.WebApi/README.md) | Localization middleware for Web API |
| [`Core.Mailing`](Core.Mailing/README.md) | Email abstraction (IMailService, Mail, MailSettings, MailTlsMode) |
| [`Core.Mailing.MailKit`](Core.Mailing.MailKit/README.md) | MailKit SMTP implementation with DKIM + CRLF guard |
| [`Core.ElasticSearch`](Core.ElasticSearch/README.md) | Elasticsearch (NEST) integration |
| [`Core.Translation.Abstraction`](Core.Translation.Abstraction/README.md) | ITranslationService abstraction |
| [`Core.Translation.AmazonTranslate`](Core.Translation.AmazonTranslate/README.md) | AWS Translate implementation |
| [`Core.Translation.AmazonTranslate.DependencyInjection`](Core.Translation.AmazonTranslate.DependencyInjection/README.md) | DI registration |
| [`Core.Outbox`](Core.Outbox/README.md) | Transactional outbox — `OutboxMessage` entity, `IOutboxStore`/`IOutboxPublisher`, `EfOutboxStore<TDbContext>` |
| [`Core.Outbox.DependencyInjection`](Core.Outbox.DependencyInjection/README.md) | `AddOutbox<TDbContext>()` — store + `OutboxPublisherWorker` BackgroundService |
| [`Core.Test`](Core.Test/README.md) | Test helpers (`BaseFakeData`, `MockRepositoryHelper`, `BaseMockRepository`) + framework regression suite (`dotnet test`). Test SDK refs `PrivateAssets="all"` — consumers sadece helpers'ı alır. |

---

## Key Features

### MediatR Pipeline Behaviors
Request pipeline with composable behaviors — add to your command/query by implementing the marker interface:

| Behavior | Interface | What it does |
|---|---|---|
| `AuthorizationBehavior` | `ISecuredRequest` | Role-based authorization, SuperAdmin bypass |
| `RequestValidationBehavior` | — (auto) | FluentValidation |
| `CachingBehavior` | `ICachableRequest` | Distributed cache with tenant-aware keys |
| `CacheRemovingBehavior` | `ICacheRemoverRequest` | Cache group invalidation |
| `TransactionScopeBehavior` | `ITransactionalRequest` | TransactionScope wrapper |
| `LoggingBehavior` | `ILoggableRequest` | Request/response logging with tenant_id |
| `PerformanceBehavior` | `IIntervalRequest` | Slow request detection |
| `TenantValidationBehavior` | `ITenantValidationRequest` | Requires valid tenant context |

### Repository Pattern
```csharp
// Your repository
public class ProductRepository : EfRepositoryBase<Product, Guid, AppDbContext>
{
    public ProductRepository(AppDbContext context, ITenantEntitySetter? tenantSetter = null)
        : base(context, tenantSetter) { }
}
```

### Multi-Tenancy
See [TENANT.md](./TENANT.md) for full multi-tenancy documentation.

### Authentication & Impersonation
See [AUTH.md](./AUTH.md) for the single-endpoint authentication system (server dispatches between tenant users and PlatformAdmin), refresh-token flow, impersonation flow, and consuming app implementation guide.

---

## Getting Started

> **Tam kurulum rehberi:** [**SETUP.md**](./SETUP.md) — `appsettings.json`, `Program.cs`,
> `DbContext`, `ITenantService`, pipeline behavior'lar ve middleware sırası dahil. Aşağıdaki
> bölüm hızlı bir özet.

### 1. Install (via project reference or NuGet)
```xml
<PackageReference Include="NetCoreBackend.NArchitecture.Core.Application" Version="1.0.0" />
<PackageReference Include="NetCoreBackend.NArchitecture.Core.MultiTenancy" Version="1.0.0" />
```

### 2. Register services
```csharp
builder.Services.AddMultiTenancy();          // ITenantContext, TenantContext (scoped)
builder.Services.AddScoped<ITenantService, YourTenantService>();

// MediatR + all pipeline behaviors in one call
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddNArchitecturePipelineBehaviors();
```

Or, cherry-pick individual behaviors (e.g., only Authorization + Caching):
```csharp
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
```

### 3. Middleware pipeline order
```csharp
app.UseRouting();
app.UseAuthentication();   // JWT parsing first
app.UseMultiTenancy();     // then tenant resolution
app.UseAuthorization();
app.MapControllers();
```

---

## Requirements

- .NET 10 SDK
- EF Core 10.x compatible database provider
- (Optional) Redis for distributed caching
- (Optional) Elasticsearch for search
- (Optional) AWS credentials for translation

---

## Project status

| Aspect | State |
|---|---|
| Production-readiness | All known critical bugs and security issues addressed (see git log) |
| Framework-level unit tests | **Not included** — consuming apps are expected to test their own use of the framework. `Core.Test` ships helpers (`BaseFakeData`, `MockRepositoryHelper`, `BaseMockRepository`) so consumer test projects can mock repositories without ceremony. |
| Elasticsearch client | Elastic.Clients.Elasticsearch 8.x with native System.Text.Json (migrated from NEST 7.x). |
| Newtonsoft.Json | Only used by `Core.CrossCuttingConcerns.Exception.WebAPI` for legacy JsonConvert calls. The rest of the stack is System.Text.Json end-to-end. |

## License

MIT
