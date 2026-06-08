# NArchitecture NuGet Paketleri

Bu dokümanda `NetCoreBackend.NArchitecture.*` ailesindeki tüm NuGet paketleri, kullanım amaçları ve kurulum komutları açıklanmaktadır.

**Prefix:** Tüm paket adları `NetCoreBackend.NArchitecture.` ile başlar.  
**Version:** `1.0.0`  
**Target:** `net10.0`

---

## İçindekiler

1. [Exception Handling](#1-exception-handling)
2. [Logging](#2-logging)
3. [Persistence (Veri Erişimi)](#3-persistence)
4. [Security (JWT / Hashing / 2FA)](#4-security)
5. [Multi-Tenancy](#5-multi-tenancy)
6. [Application Layer (CQRS / MediatR)](#6-application-layer)
7. [Localization](#7-localization)
8. [Translation](#8-translation)
9. [Mailing](#9-mailing)
10. [Outbox Pattern](#10-outbox-pattern)
11. [ElasticSearch](#11-elasticsearch)
12. [Tipik Kurulum Senaryoları](#12-tipik-kurulum-senaryoları)

---

## 1. Exception Handling

### `Core.CrossCuttingConcerns.Exception`

Domain exception tipleri: `BusinessException`, `ValidationException`, `AuthorizationException`, `NotFoundException` ve soyut `ExceptionHandler` altyapısı.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception
```

**Ne zaman:** Application/Domain katmanında exception fırlatmak için. Dış bağımlılığı yoktur.

---

### `Core.CrossCuttingConcerns.Exception.WebAPI`

Global exception middleware — RFC 7807 ProblemDetails formatında hata yanıtları döner. Exception tipini HTTP status code'a map eder (400/401/404).

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebAPI
```

**Ne zaman:** ASP.NET Core WebAPI projelerinde. `Program.cs`'e `UseExceptionHandling()` eklenir.

**Otomatik gelen bağımlılıklar:**
- `Core.CrossCuttingConcerns.Exception`
- `Core.CrossCuttingConcerns.Logging` (logging modelleri)
- `Newtonsoft.Json`

---

## 2. Logging

Logging altyapısı katmanlıdır: abstraction → model → implementasyon.

### `Core.CrossCuttingConcerns.Logging.Abstraction`

`ILogger` interface'i. Tüm logging implementasyonlarının kontratı.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Abstraction
```

---

### `Core.CrossCuttingConcerns.Logging`

`LogDetail`, `LogDetailWithException`, `LogParameter`, `FileLogConfiguration` modelleri.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging
```

---

### `Core.CrossCuttingConcerns.Logging.SeriLog`

Serilog tabanlı `ILogger` implementasyonu. Abstract base class — sink seçimi için genişletilir.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.SeriLog
```

**Otomatik gelen bağımlılıklar:**
- `Core.CrossCuttingConcerns.Logging.Abstraction`
- `Core.CrossCuttingConcerns.Logging`
- `Serilog`

---

### `Core.CrossCuttingConcerns.Logging.Serilog.File`

Serilog **file sink** implementasyonu. Günlük rolling log, 50 MB sınır, servis bazlı klasör yapısı.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Serilog.File
```

**Otomatik gelen bağımlılıklar:**
- `Core.CrossCuttingConcerns.Logging.SeriLog`
- `Serilog.Sinks.File`

---

### `Core.CrossCuttingConcerns.Logging.DependencyInjection`

`AddLogging(ILogger logger)` extension metodu — seçilen logger'ı DI container'a singleton olarak kaydeder.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.DependencyInjection
```

---

## 3. Persistence

### `Core.Persistence`

EF Core repository pattern. `Entity<TId>`, `TenantEntity<TId>`, soft delete, dynamic filtering, bulk operations, stored procedure desteği.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Persistence
```

**Otomatik gelen bağımlılıklar:**
- `Microsoft.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.Relational`
- `System.Linq.Dynamic.Core`

**Not:** Provider (SQL Server, PostgreSQL vb.) ayrıca yüklenir — bu paket provider-agnostic'tir.

---

### `Core.Persistence.DependencyInjection`

`AddDbMigrationApplier<TDbContext>()` extension metodu — startup'ta migration otomatik uygulama.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Persistence.DependencyInjection
```

---

### `Core.Persistence.WebApi`

`UseDbMigrationApplier()` middleware — uygulama açılışında EF Core migration'larını otomatik uygular.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Persistence.WebApi
```

**Ne zaman:** WebAPI projesinde startup migration kontrolü için.

---

## 4. Security

### `Core.Security`

JWT üretimi/doğrulaması, PBKDF2 şifre hashing, Email/OTP 2FA, AES-256-GCM şifreleme, RefreshToken rotasyonu, multi-tenant token claim'leri.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Security
```

**Otomatik gelen bağımlılıklar:**
- `Core.Persistence`
- `Core.MultiTenancy`
- `Microsoft.IdentityModel.Tokens`
- `System.IdentityModel.Tokens.Jwt`
- `Otp.NET`

---

### `Core.Security.DependencyInjection`

`AddSecurityServices<TUserId, TOperationClaimId, TRefreshTokenId>()` — `JwtHelper`, `EmailAuthenticatorHelper`, `OtpNetOtpAuthenticatorHelper`'ı DI'a kaydeder.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Security.DependencyInjection
```

---

### `Core.Security.WebApi.Swagger`

Swashbuckle operation filter — Swagger UI'a JWT Bearer token girişi ekler (kilit ikonu).

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Security.WebApi.Swagger
```

**Otomatik gelen bağımlılıklar:**
- `Swashbuckle.AspNetCore.SwaggerGen`

---

## 5. Multi-Tenancy

### `Core.MultiTenancy`

Tenant çözümleme (JWT claim → `X-Tenant-ID` header → subdomain), `ITenantContext`, `TenantMiddleware`, SuperAdmin impersonation.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.MultiTenancy
```

**Otomatik gelen bağımlılıklar:**
- `Core.Persistence`

**Ne zaman:** Uygulamada birden fazla tenant olacaksa. `UseAuthentication()`'dan sonra `UseMultiTenancy()` pipeline'a eklenir.

---

## 6. Application Layer

### `Core.Application`

CQRS pipeline behavior'ları: `AuthorizationBehavior`, `RequestValidationBehavior` (FluentValidation), `CachingBehavior`, `LoggingBehavior`, `TransactionScopeBehavior`, `PerformanceBehavior`, `TenantValidationBehavior`.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Application
```

**Otomatik gelen bağımlılıklar:**
- `Core.Security`
- `Core.MultiTenancy`
- `Core.CrossCuttingConcerns.Exception`
- `Core.CrossCuttingConcerns.Logging`
- `MediatR`
- `FluentValidation`
- `FluentValidation.DependencyInjectionExtensions`

**Ne zaman:** Application katmanında MediatR kullanırken — tüm cross-cutting behavior'lar bu paketle gelir.

---

## 7. Localization

### `Core.Localization.Abstraction`

`ILocalizationService` interface'i. Tüm localization implementasyonlarının kontratı.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.Abstraction
```

---

### `Core.Localization.Resource.Yaml`

YAML dosyası tabanlı localization. `Features/*/Resources/Locales/*.{culture}.yaml` pattern, lazy-loading, `en` fallback.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml
```

**Otomatik gelen bağımlılıklar:**
- `YamlDotNet`

---

### `Core.Localization.Resource.Yaml.DependencyInjection`

`AddYamlResourceLocalization()` — YAML localization'ı scoped `ILocalizationService` olarak kaydeder.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml.DependencyInjection
```

---

### `Core.Localization.Translation`

Dinamik çeviri tabanlı localization. Statik YAML yerine `ITranslationService` üzerinden gerçek zamanlı çeviri.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.Translation
```

**Otomatik gelen bağımlılıklar:**
- `Core.Localization.Abstraction`
- `Core.Translation.Abstraction`

---

### `Core.Localization.WebApi`

`LocalizationMiddleware` — `Accept-Language` header'ını okur, `ILocalizationService.AcceptLocales`'i set eder. `UseMultiTenancy()`'den sonra çalışmalı.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.WebApi
```

---

## 8. Translation

### `Core.Translation.Abstraction`

`ITranslationService` interface'i: `TranslateAsync(text, to, from="en")`.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Translation.Abstraction
```

---

### `Core.Translation.AmazonTranslate`

AWS Translate implementasyonu. 75+ dil desteği, BCP 47 dil kodları.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Translation.AmazonTranslate
```

**Otomatik gelen bağımlılıklar:**
- `AWSSDK.Translate`

---

### `Core.Translation.AmazonTranslate.DependencyInjection`

`AddAmazonTranslation(AmazonTranslateConfiguration)` — AWS Translate'i transient `ITranslationService` olarak kaydeder.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Translation.AmazonTranslate.DependencyInjection
```

---

## 9. Mailing

### `Core.Mailing`

`IMailService` abstraction ve `Mail` modeli. Subject, HTML body, to/cc/bcc, attachment, DKIM, SMTP configuration.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Mailing
```

**Otomatik gelen bağımlılıklar:**
- `MimeKit`

---

### `Core.Mailing.MailKit`

MailKit SMTP implementasyonu. DKIM signing, CRLF injection koruması, per-send connection.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Mailing.MailKit
```

**Otomatik gelen bağımlılıklar:**
- `Core.Mailing`
- `MailKit`

---

## 10. Outbox Pattern

### `Core.Outbox`

Transactional Outbox pattern — domain event ile DB write atomik. Exponential backoff retry, poison-pill yönetimi, multi-tenant desteği.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Outbox
```

**Otomatik gelen bağımlılıklar:**
- `Core.Persistence`
- `Microsoft.EntityFrameworkCore.Relational`

---

### `Core.Outbox.DependencyInjection`

`AddOutbox<TDbContext>(opt)` — `IOutboxStore`, `OutboxOptions`, `OutboxPublisherWorker` (background service) kaydeder.

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Outbox.DependencyInjection
```

**Not:** `IOutboxPublisher` implementasyonu (RabbitMQ, Kafka vb.) ayrıca yazılıp kaydedilir.

---

## 11. ElasticSearch

### `Core.ElasticSearch`

`IElasticSearch` interface ile index yönetimi, CRUD ve arama operasyonları (field-based, simple query string).

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.ElasticSearch
```

**Otomatik gelen bağımlılıklar:**
- `Elastic.Clients.Elasticsearch`

---

## 12. Tipik Kurulum Senaryoları

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

### CQRS Application Layer ekleme

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Application
```

`Core.Application` `Core.Security` ve `Core.MultiTenancy`'yi getirir, ayrıca eklemeye gerek yoktur.

---

### YAML Localization ekleme

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml.DependencyInjection
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.WebApi
```

---

### Outbox + RabbitMQ/Kafka ekleme

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Outbox.DependencyInjection
# Ardından kendi IOutboxPublisher implementasyonunu yaz ve kaydet.
```

---

### E-posta gönderimi

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Mailing.MailKit
```

---

### AWS Translate ile dinamik çeviri

```bash
dotnet add package NetCoreBackend.NArchitecture.Core.Translation.AmazonTranslate.DependencyInjection
dotnet add package NetCoreBackend.NArchitecture.Core.Localization.Translation
```

---

## Paket Bağımlılık Özeti

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
