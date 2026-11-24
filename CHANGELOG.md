# Changelog

All notable changes to this project will be documented in this file.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)  
Versioning: [Semantic Versioning](https://semver.org/)

---

## [2026-11-24] (2)

### Core.Payments 1.0.0-preview.8, Iyzico preview.5, PayTr preview.3, Simulator preview.7 — instalments and the daily transaction report

#### Changed

- **iyzico (breaking for callers that relied on it):** `NotificationVerification.Amount` and `PaymentStatusResult.Amount` are now the
  basket `price`, not `paidPrice`. An instalment payment with a fee the customer paid would have looked like an amount mismatch.

#### Added

- `NotificationVerification.InstallmentFee`; `PaymentStatusResult.InstallmentFee` and `Installment` (init-only). iyzico: `paidPrice − price`
  (both signed); PayTR: `total_amount − payment_amount` (notification) and `payment_total`, `taksit` (status query); simulator: card ending
  `0009` (3 instalments, 10% fee, signed `installment_fee`).
- `ITransactionReportProvider`, `ProviderTransaction`, `ProviderTransactionKind`, `TransactionReportResult`,
  `PaymentCapabilities.TransactionReport`, `PaymentLogStep.TransactionReport` (23). iyzico: `GET /v2/reporting/payment/transactions`
  (paged, IYZWSv2 over the path only, failed payments left out); PayTR: "İşlem Dökümü" (`/rapor/islem-dokumu`, from the docs); simulator:
  the day's payments, cancels and refunds. `IyzicoOptions.TransactionReportPath`, `PayTrOptions.TransactionReportPath`.
- The simulator's message signature also covers `installment_fee`.
- Tests: iyzico 7, PayTR 4, simulator 2, sandbox 1 (the report read from the sandbox).

---

## [2026-11-24]

### Core.Payments 1.0.0-preview.7 — a dispute won after a partial refund

#### Changed

- `PaymentOrderTransitions`: `Disputed → PartiallyRefunded` is allowed, so a card dispute won on a partly refunded order goes back to where it
  was (it could only go back to `Paid`). Test: `PaymentOrderTransitionTests` (4 cases).

---

## [2026-11-23] (3)

### Core.Payments.Simulator 1.0.0-preview.6 — the stored card's keys are starred in the log

#### Fixed

- preview.5 named the card's owner key `card_owner_key` / `ownerKey` and answered a delete with the card token, so the payment log showed
  them (found by the application's E2E leak test). The simulator now uses iyzico's names (`card_user_key` / `cardUserKey`, `card_token` /
  `cardToken`), which the log stars, and a delete answers with its own reference. Test: `PaymentSimulatorTests` (1).

---

## [2026-11-23] (2)

### Core.Payments 1.0.0-preview.6, Core.Payments.Iyzico 1.0.0-preview.4, Core.Payments.Simulator 1.0.0-preview.5 — stored cards

#### Added

- `IStoredCardPaymentProvider` (`ChargeStoredCardAsync`, `DeleteStoredCardAsync`), `StoredCardCharge`, `StoredCardReference`,
  `StoredCardDetails`. `PaymentStartRequest.StoreCard` and `StoredCardOwnerKey`; `NotificationVerification.StoredCard`. Init-only
  properties: existing callers and `IPaymentProvider` implementers (PayTR) are unchanged. `PaymentLogStep.StoredCardCharge` (21) and
  `StoredCardDeleted` (22); `cardUserKey` is starred in the payment log.
- iyzico: `Recurring`. The Checkout Form result's card keys, `cardUserKey` on the start, `/payment/auth` without 3D for a renewal
  (signature checked; a declined card is a failed payment), `DELETE /cardstorage/card`. Paths in `IyzicoOptions`
  (`StoredCardChargePath`, `StoredCardPath`).
- Simulator: `Recurring`; stored cards, charges and deletes; card ending `0008` (paid, declined on renewal). The message signature now
  covers `card_user_key` and `card_token`.
- Tests: `IyzicoStoredCardTests` (10), simulator (4), sandbox (2: a stored card charged, signed and deleted; a declined one).

---

## [2026-11-23]

### Core.Payments.Iyzico 1.0.0-preview.3 — a declined card is a failed payment

#### Fixed

- A declined card (e.g. `10051` insufficient funds) is answered by iyzico with `status: "failure"`, the `paymentId` and
  `paymentStatus: "FAILURE"`, and no signature. `VerifyNotificationAsync` and `QueryAsync` read it as a failed payment (with its error
  code) instead of a refused message or a failed query, so the order no longer waits until it expires. It is believed unsigned: the answer
  comes from our own HTTPS call, and a failure gives nothing away; a paid result still needs its signature. An error without a payment
  status (bad token, wrong keys) is unchanged.
- Tests: `Core.Test/Payments/IyzicoPaymentProviderTests` (4 new).

---

## [2026-11-21] (2)

### Core.Payments 1.0.0-preview.3 — calendar periods and proration

#### Added

- `Billing.BillingPeriod` (Monthly, Annual) and `BillingCalendar.EndOf(start, period, anchorDay, timeZone)`: the anchor day of the next
  month (or that month's last day; 31 January → 28/29 February → 31 March) or the same day next year, counted in the given time zone;
  `AnchorDayOf(start, timeZone)`.
- `Proration.UpgradePrice(currentPeriodPrice, newPeriodPrice, remaining, periodLength)`: the difference per period for the time left,
  in minor units; nothing when no time is left; a cheaper edition is refused.
- Tests: `Core.Test/Payments/BillingTests` (13).

---

## [2026-11-21]

### Core.Payments 1.0.0-preview.2, Core.Payments.Simulator 1.0.0-preview.2 — found while the application consumed them

#### Added

- `IPaymentProvider.ReferenceOf(IncomingPaymentMessage)` → `PaymentMessageReference(OrderNo, ProviderToken)`: which order a message
  names, read before its signature is checked (the signature needs that order's merchant account). Each provider knows its own field
  (simulator `order_no`; PayTR `merchant_oid`, iyzico a token later). **Breaking for implementers** (preview).

#### Fixed

- The simulator's `StartAsync` returns the payment page address (`{PageBaseUrl}/{token}`) in `PaymentPageUrl`, like a real provider;
  it was empty.

---

## [2026-11-20]

### Core.Payments 1.0.0-preview.1, Core.Payments.Simulator 1.0.0-preview.1 — payments (new, preview)

Provider-independent payment building blocks and a simulator, so an application can run its whole payment flow without a real POS.
Preview until each provider is verified in its sandbox (`--prerelease`).

#### Added

- `IPaymentProvider` (start, verify a notification without a network call, query, cancel, refund) with request and result types;
  provider errors are results, not exceptions.
- `Money` (minor units, one currency, rounding away from zero) and `VatSplit` (VAT-inclusive and -exclusive; net + VAT = total).
- `PaymentOrderTransitions`: the order status table and the notification decision (paid; review on any amount, currency, order number
  or merchant mismatch; late success after failed or abandoned; repeated notification has no effect; bad signature rejected).
- `PaymentDataMasker`: card numbers removed (Luhn, also in free text), secrets starred, personal data moved out of the body.
- Payment transaction log: `PaymentLogEntry` (20 steps), `IPaymentLogWriter`, `PaymentLogScope`, and `PaymentLoggingHandler`
  (`AddPaymentLogging()`): a row before each provider call (a failed write stops the call), a row after the answer or failure;
  header allow-list; a call outside a scope is refused.
- Simulator: hosted-form and server-notification flows over an in-process HTTP gateway (so calls are logged like real ones),
  HMAC-SHA256 signatures, magic card endings (0000, 0002–0007), `ISimulatorControl` (page, submit, held/double/tampered notification,
  close the day); refused in Production.

#### Notes for consumers

- Docs: `PAYMENTS.md`, package READMEs. Tests: `Core.Test/Payments`.

---

## [2026-11-19]

### Core.CrossCuttingConcerns.Exception 1.2.0, Core.CrossCuttingConcerns.Exception.WebApi 1.2.0, Core.MultiTenancy 2.1.0 — 403 reasons

A client can tell why a request was refused (a deactivated tenant, an expired subscription) from a missing permission.

#### Added

- `ForbiddenException.Code` (optional, `init`): written to the ProblemDetails as `code`. Without it the 403 body is unchanged.
- `ForbiddenProblemDetails(detail, code)`.

#### Changed

- `TenantMiddleware`: an inactive tenant's 403 is now `application/problem+json` (`title` "Forbidden", the same `detail` text,
  `code` "TenantInactive", constant `TenantMiddleware.TenantInactiveCode`) instead of plain text. Clients that read the plain text
  body should read `detail`.

#### Notes for consumers

- Tests: `Core.Test/CrossCuttingConcerns/ForbiddenExceptionTests.cs`, `Core.Test/MultiTenancy/TenantMiddlewareTests.cs`.

---

## [2026-11-18]

### Core.Application 3.2.0 — feature requirements

A request can say "run only when the tenant's package includes these features" (editions with limits and on/off modules).
Core asks; the app owns the feature definitions, the editions and the resolution order.

#### Added

- `IFeatureChecker.GetValueAsync(tenantId, featureName)` (implemented by the app) and `IsEnabledAsync` (only `"true"`, any case).
- `IFeatureRequest { string[] RequiredFeatures }` and `FeatureRequirementBehavior`, registered by `AddNArchitecturePipelineBehaviors`
  after `TenantValidationBehavior`. A disabled feature, or no tenant, throws `ForbiddenException` (403). The host (super admin, not
  impersonating) is not checked; impersonating a tenant applies that tenant's features.

#### Notes for consumers

- No change for apps that do not use `IFeatureRequest`: the behavior is built only for marked requests, so no `IFeatureChecker`
  registration is needed (covered by a test).
- Tests: `Core.Test/Application/FeatureRequirementBehaviorTests.cs`.

---

## [2026-11-16]

### Core.CrossCuttingConcerns.Exception 1.1.0, Core.CrossCuttingConcerns.Exception.WebApi 1.1.0, Core.Application 3.1.0 — 401 / 403 split

401 now means only "the caller is not known" (a client refreshes its token and retries); "known but not allowed" is 403,
which a new token cannot fix.

#### Added

- `ForbiddenException : AuthorizationException` (Exception). Existing `catch (AuthorizationException)` blocks still see it.
- `ExceptionHandler.HandleException(ForbiddenException)`: **virtual**, defaults to the `AuthorizationException` handler, so a
  handler written before this release compiles and answers as before.
- `HttpExceptionHandler` maps `ForbiddenException` to 403 with `ForbiddenProblemDetails` (`title`: "Forbidden") (Exception.WebApi).

#### Changed

- `AuthorizationBehavior`: a missing role throws `ForbiddenException` (403 with Exception.WebApi 1.1.0). No identity still
  throws `AuthorizationException` (401). `TenantValidationBehavior` is unchanged (401): it can be reached with no identity.

#### Notes for consumers

- **HTTP behaviour change:** with Exception.WebApi 1.1.0, a request denied by a role check answers 403 instead of 401. A
  client that refreshed its token on every 401 stops doing so for these requests. With an older Exception.WebApi the answer
  stays 401.
- Throw `ForbiddenException` for your own "known but not allowed" rules; keep `AuthorizationException` for "who are you?".
- Tests: `Core.Test/CrossCuttingConcerns/ForbiddenExceptionTests.cs`.

---

## [2026-11-15]

### Core.EntityChanges 1.0.0 — New package

An entity change log: which configured record changed, how and by whom, with the old and new value of each
allow-listed property, written in the same `SaveChanges` as the change.

#### Added

- `Entities/EntityChange` and the stored enums `EntityChangeType`, `EntityChangeActorType`, `EntityChangeImpersonationKind`.
- `EfPersistence/EfEntityChangeModelExtensions.ConfigureEntityChanges(modelBuilder, tableName)`: bounded columns, three indexes, no query filter.
- `Abstractions/IEntityChangeConfiguration<T>` and `EntityChangeTypeBuilder<T>`: `HasName`, `ScopeBy`, `IdBy`, `Track`, `Mask` (property-level secret: "changed", no value, no hash), `FollowWhen`, `MaskAllWhen`.
- `Model/EntityChangeModel`: `FromAssemblies`/`FromConfigurations` (refuses a configuration with no scope, no id, a repeated property, a nested property, or a repeated type or name), `Collect(entries)`.
- `Values/EntityChangeValues`, `PropertyChange`: language-neutral values cut at 512 characters; the JSON row shape.
- `Relations/RelationChangeTracker.TrackAsync`: before/after set comparison for bulk relation writes the change tracker cannot see.

#### Notes for consumers

- Allow list only: there is no "all properties" option; an entity without a configuration is never recorded.
- The consumer keeps the migration, the tenant visibility filter, the append-only guard and the actor.
- Tests: `Core.Test/EntityChanges/` (values and JSON contract, collector, model rules, relation tracker, table configuration).

---

## [2026-10-26]

### Core.CrossCuttingConcerns.Exception.WebApi 1.0.2

#### Fixed

- **`ExceptionMiddleware` concurrency bug** — the middleware is a single instance shared by all requests, but it kept one `HttpExceptionHandler` whose `Response` property was set per request. Two requests failing at the same time could overwrite each other's response: one request's problem details were written to another request, or the connection was dropped (`ArgumentNullException: Value cannot be null. (Parameter 'array')` from `ExceptionMiddleware.Invoke`, "response ended prematurely" on the client). Each failure now uses its own handler. No API change; `HandleExceptionAsync` / `LogException` stay `protected virtual`.
- Regression test: `Core.Test/CrossCuttingConcerns/ExceptionMiddlewareConcurrencyTests` (2000 concurrent failing requests, each must get its own status and body).

---

## [2026-07-17]

### Core.Security 3.1.0

Building blocks for **user-level impersonation** ("login as this user"): issuing a token whose
primary identity is the target user while the real actor travels in dedicated audit claims.
Additive only — no existing signature changed.

#### Added

- **`ITokenHelper.CreateToken` overload** — `CreateToken(user, operationClaims, additionalClaims, expirationMinutes = null)`: issues a tenant-user token enriched with arbitrary extra claims (e.g. impersonator identity, a session id for denylisting) and an optional per-call lifetime override (impersonation tokens are typically short-lived). The existing two-parameter overload is unchanged.
- **`ImpersonationClaimTypes`** (`Core.Security.Constants`) — `impersonator_id`, `impersonator_type` (values: `ImpersonatorTypes.PlatformAdmin` / `TenantUser`), `impersonator_tenant_id`. Publishing the names in the package prevents claim-string drift between producer and consumers. Distinct from tenant impersonation (`TenantClaimTypes.IsImpersonating`), where the identity stays the platform admin and only the data scope changes.
- **`ClaimsPrincipalExtensions`** — `IsUserImpersonation()` (true when the token carries `impersonator_id`) and `GetImpersonatorIdClaim()`.

#### Notes for consumers

- Recommended pattern: the impersonation token carries the **target user's** identity and real
  effective claims; never emit `is_super_admin` on it, so query filters and authorization behave
  as a normal tenant-user request. Do not issue a refresh token for impersonation sessions;
  revocation before expiry requires a consumer-side denylist (e.g. Redis keyed by a session-id claim).

---

## [2026-06-23]

### Core.Mediation 1.0.0 — New package

A minimal, license-free in-process mediator that replaces the subset of MediatR the framework
used. MediatR 12.4+ requires a commercial license; this package removes that dependency while
preserving every behavior the codebase relies on (~150 LOC).

#### Added

- **`Core.Mediation.Abstractions` namespace** — request/handler contracts: `IRequest<TResponse>`, non-generic `IRequest` (`: IRequest<Unit>`), `IRequestHandler<TRequest, TResponse>`, single-parameter `IRequestHandler<TRequest>` (void commands, bridged to `Unit` via a Default Interface Method), `IPipelineBehavior<TRequest, TResponse>`, `RequestHandlerDelegate<TResponse>`, `Unit`, and `IMediator`.
- **`Core.Mediation` namespace** — the runtime `Mediator` dispatcher: resolves the handler and all opt-in `IPipelineBehavior<,>` from DI, composes them into a nested pipeline (registration order = execution order), and caches the per-request-type wrapper in a `ConcurrentDictionary`.
- **`Core.Mediation.DependencyInjection` namespace** — `AddMediator(params Assembly[])`, the drop-in replacement for `AddMediatR(...)`. Registers `IMediator` as scoped and scans assemblies for `IRequestHandler<,>` implementations.
- Single dependency: `Microsoft.Extensions.DependencyInjection.Abstractions`.

---

### Core.Application 3.0.0

#### Breaking Changes

- **MediatR dependency removed.** The CQRS/pipeline contracts (`IRequest`, `IRequestHandler`, `IPipelineBehavior`, `IMediator`, `Unit`, `RequestHandlerDelegate`) now come from the new `Core.Mediation` package instead of the `MediatR` namespace.

  **Migration guide:** in every file that referenced these types, replace the import:

  ```diff
  - using MediatR;
  + using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;
  ```

  Where `AddMediatR(...)` was called, switch to `AddMediator(...)` and add `using NetCoreBackend.NArchitecture.Core.Mediation.DependencyInjection;`:

  ```diff
  - services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
  + services.AddMediator(assembly);
  ```

  Behavior is unchanged: pipeline execution order, opt-in marker interfaces, and the void-command path all work exactly as before. Void command handlers must implement **only** `Task Handle(...)` — the two-parameter `Task<Unit> Handle(...)` is supplied by the DIM bridge and must not be hand-implemented.

#### Changed

- `Core.Application` now depends on `Core.Mediation` (project/package reference) instead of `MediatR`. Consumers receive `Core.Mediation` transitively.

> **Publish order:** publish `Core.Mediation 1.0.0` first, then `Core.Application 3.0.0` (the latter depends on the former).

---

### Documentation — all package READMEs translated to English (patch releases)

Every package README (and the root guides, scripts, and this changelog) was translated from Turkish to English. Because a README is embedded in its `.nupkg`, each affected package received a **patch bump** so the English README reaches NuGet (a re-push at the same version is skipped by `--skip-duplicate`). No code changed in these packages — docs only.

- `1.0.0 → 1.0.1`: Core.CrossCuttingConcerns.Exception, Core.CrossCuttingConcerns.Exception.WebAPI, Core.CrossCuttingConcerns.Logging, Core.CrossCuttingConcerns.Logging.Abstraction, Core.CrossCuttingConcerns.Logging.DependencyInjection, Core.CrossCuttingConcerns.Logging.SeriLog, Core.CrossCuttingConcerns.CorrelationId, Core.CrossCuttingConcerns.CorrelationId.WebApi, Core.Localization.Abstraction, Core.Localization.Translation, Core.Localization.Resource.Yaml, Core.Localization.Resource.Yaml.DependencyInjection, Core.Localization.WebApi, Core.Translation.Abstraction, Core.Translation.AmazonTranslate, Core.Translation.AmazonTranslate.DependencyInjection, Core.Mailing, Core.Mailing.MailKit, Core.ElasticSearch, Core.Outbox, Core.Outbox.DependencyInjection, Core.Persistence.DependencyInjection, Core.Persistence.WebApi, Core.Security.DependencyInjection, Core.Security.WebApi.Swagger
- `1.0.1 → 1.0.2`: Core.CrossCuttingConcerns.Logging.Serilog.File
- `1.1.1 → 1.1.2`: Core.Persistence
- `2.0.0 → 2.0.1`: Core.MultiTenancy
- `3.0.0 → 3.0.1`: Core.Security

> Core.Application (3.0.0) and Core.Mediation (1.0.0) already ship their English READMEs with the versions above — no extra docs bump needed.

---

## [3.0.0] - 2026-06-12

### Core.Security 3.0.0

#### Breaking Changes

- **`ITokenHelper<TUserId, TOperationClaimId, TRefreshTokenId>` has a new method: `CreateAdminRefreshToken`.**

  Any custom implementation of `ITokenHelper` must add this method:

  ```csharp
  AdminRefreshToken<TRefreshTokenId, TUserId> CreateAdminRefreshToken(PlatformAdmin<TUserId> admin, string ipAddress);
  ```

  `JwtHelper` already implements it. If you are injecting (not implementing) `ITokenHelper`, no action is needed.

#### Added

- **`AdminRefreshToken<TId, TAdminId>` entity** — refresh token for `PlatformAdmin`. Extends `Entity<TId>` instead of `TenantEntity<TId>` because platform admins are not tenant-scoped. Holds `AdminId` in place of `UserId`. Carries the same rotation/revocation fields (`RevokedDate`, `RevokedByIp`, `ReplacedByToken`, `ReasonRevoked`) and computed properties (`IsExpired`, `IsRevoked`, `IsActive`) as `RefreshToken`.

- **`ITokenHelper.CreateAdminRefreshToken(PlatformAdmin<TUserId> admin, string ipAddress)`** — issues a refresh token for a `PlatformAdmin`. Uses `_tokenOptions.RefreshTokenTtlDays` for expiration, same as `CreateRefreshToken` for regular users.

---

## [2.0.0] - 2026-06-11

### Core.Security 2.0.0

#### Breaking Changes

- **`PlatformAdmin<TId>` base class changed from `AuditableEntity<TId>` → `Entity<TId>`.**

  Platform admins are not application users — there is no meaningful "who created this admin" in the same way as tenant-user audit. Audit columns (`CreatedAt`, `UpdatedAt`, `CreatedById`, `UpdatedById`, `DeletedById`) are removed from the `PlatformAdmins` table.

  **Migration guide:**

  ```bash
  dotnet ef migrations add RemovePlatformAdminAuditColumns
  dotnet ef database update
  ```

  The migration will drop `CreatedAt`, `UpdatedAt`, `CreatedById`, `UpdatedById`, `DeletedById` from the `PlatformAdmins` table. No data in other tables is affected.

---

### Core.Application 2.0.0

#### Breaking Changes

- **`SuperAdminBlockBehavior<TRequest, TResponse>` removed.**

  Previously, this pipeline behavior blocked non-impersonating `PlatformAdmin` tokens from reaching handlers marked `IBlockedForSuperAdmin`. The model has changed: a `PlatformAdmin` without an active impersonation session sees **all tenants' data** (no EF Core tenant filter applies); impersonating narrows the view to a single tenant. There is no longer a category of endpoints that PlatformAdmin must be blocked from.

  **Migration guide:** Remove `IBlockedForSuperAdmin` from any handler that implements it. The interface no longer exists and will cause a compile error.

- **`IBlockedForSuperAdmin` marker interface removed.**

  Consumed only by the now-removed `SuperAdminBlockBehavior`. Any handler implementing this interface will fail to compile — remove the interface from the handler's declaration.

#### Changed

- **`AddNArchitecturePipelineBehaviors` no longer registers `SuperAdminBlockBehavior`.**

  No action needed if `AddNArchitecturePipelineBehaviors()` is called (registration is gone automatically). If the behavior was registered manually, remove it.

---

## [1.1.1] - 2026-06-10

### Core.Security 1.1.1

#### Added

- **`OperationClaim.Description`** — optional `string?` field describing what the claim grants. Nullable and backward-compatible; existing records without a description remain valid.

---

### Core.Persistence 1.1.1

#### Fixed

- **`ExecuteStoredProcedureAsync` is now provider-agnostic.** Previously hardcoded Oracle `BEGIN {procedure}; END;` syntax. Now detects the active EF Core provider at runtime via `Context.Database.ProviderName` and generates the correct syntax:
  - SQL Server → `EXEC {procedure}`
  - PostgreSQL → `CALL {procedure}`
  - Oracle / other → `BEGIN {procedure}; END;` (unchanged fallback)

---

## [1.1.0] - 2026-06-10

### Core.Persistence 1.1.0

#### Added

- **`IEntityAudit` interface** — declares `CreatedById`, `UpdatedById`, `DeletedById` (`Guid?`) for user-level audit tracking. All nullable so system operations (background jobs, migrations) without an authenticated user are handled gracefully.

- **`ICurrentUserService` interface** — abstraction for resolving the authenticated user's ID. Implement in the consuming application (e.g. via `IHttpContextAccessor` + claim reading) and register as scoped. Returns `null` when no user is authenticated.

- **`AuditableEntity<TId>` base class** — opt-in base that extends `Entity<TId>` and implements `IEntityAudit`. For entities that need audit but no tenant isolation.

- **`AuditableTenantEntity<TId>` base class** — extends `TenantEntity<TId>` and implements `IEntityAudit`. For entities that need both tenant isolation and audit tracking.

- **`EfRepositoryBase` populates audit fields automatically** — accepts optional `ICurrentUserService` in the constructor (backward-compatible; existing code without it continues to work). Audit fields are set only when the entity implements `IEntityAudit`. `CreatedById` is set on Add, `UpdatedById` on Update, `DeletedById` on soft-delete (including cascade).

#### Migration guide

1. For entities that need audit, change the base class:
   ```csharp
   // before
   public class Product : TenantEntity<Guid> { ... }
   // after
   public class Product : AuditableEntity<Guid> { ... }
   // or implement IEntityAudit directly if already using a custom base
   ```
2. Register your `ICurrentUserService` implementation in DI:
   ```csharp
   services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();
   ```
3. Add an EF Core migration only for entities that now extend `AuditableEntity<TId>`.

---

## [1.1.0] - 2026-06-10

### Core.Security 1.1.0

#### Changed

- **`User<TId>`** base class changed from `TenantEntity<TId>` → `AuditableTenantEntity<TId>`.
  Adds `CreatedById`, `UpdatedById`, `DeletedById` columns to the Users table.

- **`UserOperationClaim<TId>`** base class changed from `TenantEntity<TId>` → `AuditableTenantEntity<TId>`.
  Adds audit columns to the UserOperationClaims table. Enables tracking of who granted or revoked a permission.

- **`OperationClaim<TId>`** base class changed from `Entity<TId>` → `AuditableEntity<TId>`.
  Adds audit columns to the OperationClaims table. Enables tracking of who created or deleted a system permission definition.

- **`PlatformAdmin<TId>`** base class changed from `Entity<TId>` → `AuditableEntity<TId>`.
  Adds audit columns to the PlatformAdmins table. Tracks who created or deleted a platform-level administrator — critical for security audit trail.

#### Not changed (intentional)

- `RefreshToken` — system-managed; already tracks `CreatedByIp`/`RevokedByIp`; `CreatedById` would duplicate `UserId`.
- `EmailAuthenticator` — user sets up their own 2FA; lifecycle tracked via `ActivationKey`/`ActivationKeyExpiresAt`/`ActivationKeyConsumedAt`.
- `OtpAuthenticator` — same reasoning as `EmailAuthenticator`.

#### Migration guide

Add an EF Core migration in the consuming application to apply the new columns:

```bash
dotnet ef migrations add AddAuditFieldsToSecurityEntities
dotnet ef database update
```

This adds three nullable `Guid` columns (`CreatedById`, `UpdatedById`, `DeletedById`) to the
following tables: **Users**, **UserOperationClaims**, **OperationClaims**, **PlatformAdmins**.

Existing rows will have `NULL` in these columns — no data loss, no required backfill.

To populate audit fields going forward, register `ICurrentUserService` (from `Core.Persistence`) in the consuming application:

```csharp
services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();
```

---

## [2.0.0] - 2026-06-09

### Core.MultiTenancy 2.0.0

#### Breaking Changes

- **`ModelBuilderTenantExtensions.ApplyTenantFilters` removed.**

  `Expression.Constant(tenantContext)` captures a single `ITenantContext` instance at model-build time. EF Core caches the compiled query — all subsequent requests filter against the first request's tenant, a critical per-request isolation bug.

  **Migration:** Replace with a closure-based `HasQueryFilter` in `OnModelCreating`:

  ```csharp
  protected override void OnModelCreating(ModelBuilder builder)
  {
      builder.Entity<MyEntity>().HasQueryFilter(e =>
          (_tenantContext.IsSuperAdmin || e.TenantId == _tenantContext.TenantId)
          && e.DeletedDate == null);

      base.OnModelCreating(builder);
  }
  ```

  EF Core re-evaluates the closure on every query because `_tenantContext` is captured via the scoped `DbContext` instance. See `TENANT.md` — *EF Core Global Query Filter Setup* for details.

---

## [1.0.1] - 2026-06-09

### Core.MultiTenancy 1.0.1

#### Fixed

- `TenantMiddleware`: returns 401 when JWT carries a `tenant_id` claim but the tenant no longer exists in the database (deleted tenant).
- `TenantValidationBehavior`: now reads from `ITenantContext` instead of raw JWT claims via `IHttpContextAccessor`, correctly reflecting the tenant state resolved by middleware.

### Core.Persistence 1.0.1

#### Fixed

- `EfRepositoryBase.ApplyIncludeDeleted`: `withDeleted: true` now strips the soft-delete filter while preserving tenant isolation. Previously, `IgnoreQueryFilters()` removed both the soft-delete and tenant filters simultaneously.

---

## [1.0.0] - 2026-06-08

Initial release.
