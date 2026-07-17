# NArchitecture NuGet Packages

This document describes all NuGet packages in the `NetCoreBackend.NArchitecture.*` family, their purpose, and their installation commands.

**Prefix:** All package names start with `NetCoreBackend.NArchitecture.`.  
**Version:** `1.0.0`  
**Target:** `net10.0`

---

## Table of Contents

1. [Exception Handling](#1-exception-handling)
2. [Logging](#2-logging)
3. [Persistence (Data Access)](#3-persistence)
4. [Security (JWT / Hashing / 2FA)](#4-security)
5. [Multi-Tenancy](#5-multi-tenancy)
6. [Application Layer (CQRS / MediatR)](#6-application-layer)
7. [Localization](#7-localization)
8. [Translation](#8-translation)
9. [Mailing](#9-mailing)
10. [Outbox Pattern](#10-outbox-pattern)
11. [ElasticSearch](#11-elasticsearch)
12. [Typical Installation Scenarios](#12-typical-installation-scenarios)

---

## 1. Exception Handling

### `Core.CrossCuttingConcerns.Exception`

Domain exception types: `BusinessException`, `ValidationException`, `AuthorizationException`, `NotFoundException`, and the abstract `ExceptionHandler` infrastructure.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception
```

**When:** For throwing exceptions in the Application/Domain layer. It has no external dependencies.

---

### `Core.CrossCuttingConcerns.Exception.WebAPI`

Global exception middleware — returns error responses in RFC 7807 ProblemDetails format. Maps the exception type to an HTTP status code (400/401/404).

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebAPI
```

**When:** In ASP.NET Core WebAPI projects. `UseExceptionHandling()` is added to `Program.cs`.

**Transitive dependencies:**
- `Core.CrossCuttingConcerns.Exception`
- `Core.CrossCuttingConcerns.Logging` (logging models)
- `Newtonsoft.Json`

---

## 2. Logging

The logging infrastructure is layered: abstraction → model → implementation.

### `Core.CrossCuttingConcerns.Logging.Abstraction`

The `ILogger` interface. The contract for all logging implementations.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Abstraction
```

---

### `Core.CrossCuttingConcerns.Logging`

The `LogDetail`, `LogDetailWithException`, `LogParameter`, `FileLogConfiguration` models.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging
```

---

### `Core.CrossCuttingConcerns.Logging.SeriLog`

Serilog-based `ILogger` implementation. An abstract base class — extended to choose a sink.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.SeriLog
```

**Transitive dependencies:**
- `Core.CrossCuttingConcerns.Logging.Abstraction`
- `Core.CrossCuttingConcerns.Logging`
- `Serilog`

---

### `Core.CrossCuttingConcerns.Logging.Serilog.File`

Serilog **file sink** implementation. Daily rolling log, 50 MB limit, per-service folder structure.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Serilog.File
```

**Transitive dependencies:**
- `Core.CrossCuttingConcerns.Logging.SeriLog`
- `Serilog.Sinks.File`

---

### `Core.CrossCuttingConcerns.Logging.DependencyInjection`

The `AddLogging(ILogger logger)` extension method — registers the chosen logger in the DI container as a singleton.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.DependencyInjection
```

---

## 3. Persistence

### `Core.Persistence`

EF Core repository pattern. `Entity<TId>`, `TenantEntity<TId>`, soft delete, dynamic filtering, bulk operations, stored procedure support.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Persistence
```

**Transitive dependencies:**
- `Microsoft.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.Relational`
- `System.Linq.Dynamic.Core`

**Note:** The provider (SQL Server, PostgreSQL, etc.) is installed separately — this package is provider-agnostic.

---

### `Core.Persistence.DependencyInjection`

The `AddDbMigrationApplier<TDbContext>()` extension method — applies migrations automatically at startup.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Persistence.DependencyInjection
```

---

### `Core.Persistence.WebApi`

The `UseDbMigrationApplier()` middleware — automatically applies EF Core migrations on application startup.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Persistence.WebApi
```

**When:** For startup migration control in a WebAPI project.

---

## 4. Security

### `Core.Security`

JWT generation/validation, PBKDF2 password hashing, Email/OTP 2FA, AES-256-GCM encryption, RefreshToken rotation, multi-tenant token claims, user-impersonation claim helpers (`ImpersonationClaimTypes`, enriched `CreateToken` overload — 3.1.0).

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Security
```

**Transitive dependencies:**
- `Core.Persistence`
- `Core.MultiTenancy`
- `Microsoft.IdentityModel.Tokens`
- `System.IdentityModel.Tokens.Jwt`
- `Otp.NET`

---

### `Core.Security.DependencyInjection`

`AddSecurityServices<TUserId, TOperationClaimId, TRefreshTokenId>()` — registers `JwtHelper`, `EmailAuthenticatorHelper`, and `OtpNetOtpAuthenticatorHelper` in the DI container.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Security.DependencyInjection
```

---

### `Core.Security.WebApi.Swagger`

Swashbuckle operation filter — adds a JWT Bearer token input to the Swagger UI (the lock icon).

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Security.WebApi.Swagger
```

**Transitive dependencies:**
- `Swashbuckle.AspNetCore.SwaggerGen`

---

## 5. Multi-Tenancy

### `Core.MultiTenancy`

Tenant resolution (JWT claim → `X-Tenant-ID` header → subdomain), `ITenantContext`, `TenantMiddleware`, SuperAdmin impersonation.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.MultiTenancy
```

**Transitive dependencies:**
- `Core.Persistence`

**When:** If the application has more than one tenant. `UseMultiTenancy()` is added to the pipeline after `UseAuthentication()`.

---

## 6. Application Layer

### `Core.Application`

CQRS pipeline behaviors: `AuthorizationBehavior`, `RequestValidationBehavior` (FluentValidation), `CachingBehavior`, `LoggingBehavior`, `TransactionScopeBehavior`, `PerformanceBehavior`, `TenantValidationBehavior`.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Application
```

**Transitive dependencies:**
- `Core.Security`
- `Core.MultiTenancy`
- `Core.CrossCuttingConcerns.Exception`
- `Core.CrossCuttingConcerns.Logging`
- `MediatR`
- `FluentValidation`
- `FluentValidation.DependencyInjectionExtensions`

**When:** When using MediatR in the Application layer — all cross-cutting behaviors come with this package.

---

## 7. Localization

### `Core.Localization.Abstraction`

The `ILocalizationService` interface. The contract for all localization implementations.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.Abstraction
```

---

### `Core.Localization.Resource.Yaml`

YAML file-based localization. `Features/*/Resources/Locales/*.{culture}.yaml` pattern, lazy-loading, `en` fallback.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml
```

**Transitive dependencies:**
- `YamlDotNet`

---

### `Core.Localization.Resource.Yaml.DependencyInjection`

`AddYamlResourceLocalization()` — registers YAML localization as a scoped `ILocalizationService`.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml.DependencyInjection
```

---

### `Core.Localization.Translation`

Dynamic translation-based localization. Real-time translation via `ITranslationService` instead of static YAML.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.Translation
```

**Transitive dependencies:**
- `Core.Localization.Abstraction`
- `Core.Translation.Abstraction`

---

### `Core.Localization.WebApi`

`LocalizationMiddleware` — reads the `Accept-Language` header and sets `ILocalizationService.AcceptLocales`. Must run after `UseMultiTenancy()`.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.WebApi
```

---

## 8. Translation

### `Core.Translation.Abstraction`

The `ITranslationService` interface: `TranslateAsync(text, to, from="en")`.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Translation.Abstraction
```

---

### `Core.Translation.AmazonTranslate`

AWS Translate implementation. 75+ language support, BCP 47 language codes.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Translation.AmazonTranslate
```

**Transitive dependencies:**
- `AWSSDK.Translate`

---

### `Core.Translation.AmazonTranslate.DependencyInjection`

`AddAmazonTranslation(AmazonTranslateConfiguration)` — registers AWS Translate as a transient `ITranslationService`.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Translation.AmazonTranslate.DependencyInjection
```

---

## 9. Mailing

### `Core.Mailing`

The `IMailService` abstraction and `Mail` model. Subject, HTML body, to/cc/bcc, attachment, DKIM, SMTP configuration.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Mailing
```

**Transitive dependencies:**
- `MimeKit`

---

### `Core.Mailing.MailKit`

MailKit SMTP implementation. DKIM signing, CRLF injection protection, per-send connection.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Mailing.MailKit
```

**Transitive dependencies:**
- `Core.Mailing`
- `MailKit`

---

## 10. Outbox Pattern

### `Core.Outbox`

Transactional Outbox pattern — the domain event and DB write are atomic. Exponential backoff retry, poison-pill handling, multi-tenant support.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Outbox
```

**Transitive dependencies:**
- `Core.Persistence`
- `Microsoft.EntityFrameworkCore.Relational`

---

### `Core.Outbox.DependencyInjection`

`AddOutbox<TDbContext>(opt)` — registers `IOutboxStore`, `OutboxOptions`, and `OutboxPublisherWorker` (background service).

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Outbox.DependencyInjection
```

**Note:** The `IOutboxPublisher` implementation (RabbitMQ, Kafka, etc.) is written and registered separately.

---

## 11. ElasticSearch

### `Core.ElasticSearch`

Index management, CRUD, and search operations (field-based, simple query string) via the `IElasticSearch` interface.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.ElasticSearch
```

**Transitive dependencies:**
- `Elastic.Clients.Elasticsearch`

---

## 12. Typical Installation Scenarios

### Minimal WebAPI (Exception + Logging + Persistence + Security + MultiTenancy)

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebAPI
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Serilog.File
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.DependencyInjection
dotnet add package NetCoreBackend.NArchitecture.Core.Persistence
dotnet add package NetCoreBackend.NArchitecture.Core.Persistence.DependencyInjection
dotnet add package NetCoreBackend.NArchitecture.Core.Persistence.WebApi
dotnet add package NetCoreBackend.NArchitecture.Core.MultiTenancy
dotnet add package NetCoreBackend.NArchitecture.Core.Security
dotnet add package NetCoreBackend.NArchitecture.Core.Security.DependencyInjection
dotnet add package NetCoreBackend.NArchitecture.Core.Security.WebApi.Swagger
```

---

### Adding the CQRS Application Layer

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Application
```

`Core.Application` pulls in `Core.Security` and `Core.MultiTenancy`, so there is no need to add them separately.

---

### Adding YAML Localization

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml.DependencyInjection
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.WebApi
```

---

### Adding Outbox + RabbitMQ/Kafka

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Outbox.DependencyInjection
# Then write and register your own IOutboxPublisher implementation.
```

---

### Sending email

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Mailing.MailKit
```

---

### Dynamic translation with AWS Translate

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Translation.AmazonTranslate.DependencyInjection
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.Translation
```

---

## Package Dependency Summary

```
Core.Application
  └── Core.Security
        └── Core.Persistence
        └── Core.MultiTenancy
              └── Core.Persistence
  └── Core.MultiTenancy
  └── Core.CrossCuttingConcerns.Exception
  └── Core.CrossCuttingConcerns.Logging

Core.CrossCuttingConcerns.Exception.WebAPI
  └── Core.CrossCuttingConcerns.Exception
  └── Core.CrossCuttingConcerns.Logging

Core.CrossCuttingConcerns.Logging.Serilog.File
  └── Core.CrossCuttingConcerns.Logging.SeriLog
        └── Core.CrossCuttingConcerns.Logging.Abstraction
        └── Core.CrossCuttingConcerns.Logging

Core.Outbox.DependencyInjection
  └── Core.Outbox
        └── Core.Persistence

Core.Localization.Resource.Yaml.DependencyInjection
  └── Core.Localization.Resource.Yaml
        └── Core.Localization.Abstraction

Core.Translation.AmazonTranslate.DependencyInjection
  └── Core.Translation.AmazonTranslate
        └── Core.Translation.Abstraction

Core.Mailing.MailKit
  └── Core.Mailing

Core.Security.DependencyInjection
  └── Core.Security

Core.Persistence.DependencyInjection / Core.Persistence.WebApi
  └── Core.Persistence
```
