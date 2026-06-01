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
| `Core.Application` | MediatR pipelines, CQRS base, DTOs, business rules |
| `Core.Persistence` | EF Core repository pattern, paging, dynamic query, tenant entity base |
| `Core.Security` | JWT, refresh tokens, hashing, OTP, claim extensions |
| `Core.MultiTenancy` | Multi-tenant middleware, ITenantContext, tenant resolution |
| `Core.CrossCuttingConcerns.Exception` | Domain exception types |
| `Core.CrossCuttingConcerns.Exception.WebApi` | Global exception handler for Web API |
| `Core.CrossCuttingConcerns.Logging` | LogDetail, LogParameter models |
| `Core.CrossCuttingConcerns.Logging.Abstraction` | ILogger abstraction |
| `Core.CrossCuttingConcerns.Logging.SeriLog` | Serilog implementation |
| `Core.CrossCuttingConcerns.Logging.Serilog.File` | File sink configuration |
| `Core.CrossCuttingConcerns.Logging.DependencyInjection` | DI registration for logging |
| `Core.Localization.Abstraction` | ILocalizationService abstraction |
| `Core.Localization.Resource.Yaml` | YAML-based resource files |
| `Core.Localization.Resource.Yaml.DependencyInjection` | DI registration for YAML localization |
| `Core.Localization.Translation` | Translation pipeline |
| `Core.Localization.WebApi` | Localization middleware for Web API |
| `Core.Mailing` | Email abstraction (IMailService, MailRequest) |
| `Core.Mailing.MailKit` | MailKit implementation |
| `Core.ElasticSearch` | Elasticsearch (NEST) integration |
| `Core.Translation.Abstraction` | ITranslationService abstraction |
| `Core.Translation.AmazonTranslate` | AWS Translate implementation |
| `Core.Translation.AmazonTranslate.DependencyInjection` | DI registration |
| `Core.Test` | Base test helpers and AutoMapper test setup |

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
See [AUTH.md](./AUTH.md) for the two-track authentication system (tenant users vs PlatformAdmin), impersonation flow, and consuming app implementation guide.

---

## Getting Started

### 1. Install (via project reference or NuGet)
```xml
<PackageReference Include="NetCoreBackend.NArchitecture.Core.Application" Version="1.0.0" />
<PackageReference Include="NetCoreBackend.NArchitecture.Core.MultiTenancy" Version="1.0.0" />
```

### 2. Register services
```csharp
builder.Services.AddMultiTenancy();          // ITenantContext, TenantContext (scoped)
builder.Services.AddScoped<ITenantService, YourTenantService>();

// MediatR + Behaviors
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TenantValidationBehavior<,>));
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

## License

MIT
