# MediatR Removal — Implementation Prompt (to be given to an AI agent)

> **✅ IMPLEMENTED (2026-06-23):** Side A and Side B are complete and verified
> (`Core.Mediation 1.0.0`, `Core.Application 3.0.0`, published). See CHANGELOG.md. Historical record.

> This document is the **actionable** form of the `docs/MEDIATR_REMOVAL_ANALYSIS.md` analysis.
> You can give the instruction below to an AI agent as-is. Work spans two separate repos:
> **Side A = `NetCoreBackend/`** (library), **Side B = `NetCoreBackendApi/`** (consumer API).
>
> **ARCHITECTURAL DECISION:** The mediation code is written not inside Core.Application but into a
> **separate `Core.Mediation` NuGet package** (to follow the repo's "each concern a separate package +
> separate namespace" pattern).
> Hence there are two public namespaces:
> - `NetCoreBackend.NArchitecture.Core.Mediation.Abstractions` → contracts (IRequest, IRequestHandler, IPipelineBehavior, RequestHandlerDelegate, Unit, IMediator)
> - `NetCoreBackend.NArchitecture.Core.Mediation.DependencyInjection` → `AddMediator`
> - `NetCoreBackend.NArchitecture.Core.Mediation` (root) → `Mediator` impl + internal wrappers

---

## PROMPT (copy from here)

Your task: remove the MediatR (commercial license) dependency from the NetCoreBackend (NArchitecture) project and the NetCoreBackendApi project that consumes it, and bring online our own minimal mediator implementation that preserves behavior one-for-one. There are two sides; proceed IN ORDER and verify the build at every step.

### ⚠️ Critical rules
1. The design below is **NOT a copy of MediatR 14** — it deliberately mimics MediatR 11 semantics (`IRequest : IRequest<Unit>` + DIM bridge). **Do not look at the MediatR source and "fix" it.** Write the given code exactly.
2. Void command handlers write ONLY `Task Handle(...)`; never implement the two-parameter `Task<Unit> Handle(...)` by hand — the DIM bridge provides it.
3. Behavior must not change: pipeline execution order = DI registration order. Preserve the existing order.
4. Do not change any handler/command/behavior body; only change the `using` lines and the registration lines specified below.
5. **LOAD-BEARING ASSUMPTION — do not touch:** Behaviors are registered as open generics + an extra constraint (e.g. `AuthorizationBehavior<,> where TRequest : ISecuredRequest`). For requests that do not carry the marker interface, Microsoft DI **silently skips** the closed type that does not satisfy the constraint during `GetServices<IPipelineBehavior<TRequest,TResponse>>()` — this is the entirety of the "opt-in behavior" mechanism. The Mediator MUST resolve via `GetServices<IPipelineBehavior<TRequest,TResponse>>()`; do not "optimize" by hand-filtering/sorting the behaviors or using `GetServices(typeof(...))` reflection. Otherwise the entire pipeline (auth, validation, cache, transaction) is silently applied to every request or never applied at all.

---

## SIDE A — `NetCoreBackend/` (library)

### A0. DIM bridge verification spike (the most critical risk — after A1-A3, before A4)

The lifeblood of the whole migration is that the single-parameter void handler (`IRequestHandler<TRequest>`,
body `Task Handle`) can be resolved from DI as `IRequestHandler<TRequest, Unit>` and called through the
DIM bridge. **If this does not work, the 11 void commands silently break.**
PROVE it with an isolated test — it must be green BEFORE moving on to A4-A7.

> **Ordering note:** Because the spike test calls `AddMediator(...)`, the A1+A2+A3 files must
> exist. The correct order is: **A1 → A2 → A3 → A0 (spike test) → once green, A4-A7**.
> So the "BEFORE" in the A0 heading means "before the `using` change in the behavior files and
> before the packaging step"; A0 is still run after A1-A3.

```csharp
// Core.Test/Application/MediatorVoidHandlerTests.cs (temporary spike — uses the A1+A2+A3 types)
// usings:
//   using Microsoft.Extensions.DependencyInjection;
//   using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;       // IRequest, IRequestHandler, IMediator
//   using NetCoreBackend.NArchitecture.Core.Mediation.DependencyInjection; // AddMediator
[Fact]
public async Task Void_Handler_Resolves_And_Runs_Through_DIM_Bridge()
{
    var probe = new List<string>();

    var services = new ServiceCollection();
    services.AddMediator(typeof(MediatorVoidHandlerTests).Assembly); // A3
    services.AddSingleton(probe);
    using var sp = services.BuildServiceProvider();

    using var scope = sp.CreateScope();
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

    await mediator.Send(new PingCommand());          // IRequest (non-generic)

    Assert.Equal(new[] { "handled" }, probe);        // did the handler actually run?
}

public sealed class PingCommand : IRequest { }       // = IRequest<Unit>

public sealed class PingHandler : IRequestHandler<PingCommand>   // single-parameter, Task Handle
{
    private readonly List<string> _probe;
    public PingHandler(List<string> probe) => _probe = probe;
    public Task Handle(PingCommand request, CancellationToken ct)
    {
        _probe.Add("handled");
        return Task.CompletedTask;
    }
}
```

`dotnet test` → do not continue with the migration until this test is GREEN. (All 11 existing void handlers
have the same shape as `PingHandler` — if this test passes, they all pass.)

### A1. New project `Core.Mediation` + contract files (`Core.Mediation/Abstractions/`)

First create `Core.Mediation.csproj` (see A6 for details; the `RootNamespace=NetCoreBackend.NArchitecture.$(AssemblyName)` repo pattern automatically derives the namespace from AssemblyName=`Core.Mediation`). The contracts below go under **`Core.Mediation/Abstractions/`**, each type in its own file (generic arity-overload pairs together):

- `Abstractions/IRequest.cs` → `IRequest<TResponse>` + `IRequest`
- `Abstractions/Unit.cs` → `Unit`
- `Abstractions/RequestHandlerDelegate.cs` → `RequestHandlerDelegate<TResponse>`
- `Abstractions/IRequestHandler.cs` → `IRequestHandler<,>` + `IRequestHandler<>` (DIM bridge)
- `Abstractions/IPipelineBehavior.cs` → `IPipelineBehavior<,>`
- `Abstractions/IMediator.cs` → `IMediator`

All in the **same** namespace (`...Core.Mediation.Abstractions`). The bodies are exactly the same as the block below; only split them into files:

```csharp
namespace NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

// "This class is a request returning TResponse" — a marker, no behavior.
public interface IRequest<out TResponse> { }

// Non-generic marker — for void commands. (Our design: IRequest : IRequest<Unit>.)
public interface IRequest : IRequest<Unit> { }

// The "no value" return of void commands. A struct because it has a single value.
public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Value = default;
    public bool Equals(Unit other) => true;
    public override bool Equals(object? obj) => obj is Unit;
    public override int GetHashCode() => 0;
}

// Delegate for moving to the next step in the pipeline. Behaviors always call next(ct);
// `= default` is for compatibility with MediatR 14's actual signature (not required, harmless).
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);

// Two-parameter handler — query/command handlers that return a value.
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

// Single-parameter handler — void command handlers. The body writes `Task Handle(...)`.
// A DIM (Default Interface Method) bridges Task → Task<Unit>; the dispatcher always
// resolves and calls the two-parameter IRequestHandler<TRequest, Unit>, and the DIM steps in.
// PRECONDITION: void handler classes must NOT implement the two-parameter Handle themselves.
public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest<Unit>
{
    new Task Handle(TRequest request, CancellationToken cancellationToken);

    async Task<Unit> IRequestHandler<TRequest, Unit>.Handle(TRequest request, CancellationToken cancellationToken)
    {
        await Handle(request, cancellationToken);
        return Unit.Value;
    }
}

// Pipeline behavior contract.
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}

// The entry point that controllers inject (BaseController does GetService<IMediator>()).
// NOTE: MediatR has a separate ISender; NOTHING in this project uses ISender,
// so we did not add it per YAGNI. If needed later, splitting it into `interface ISender { Send... }` +
// `IMediator : ISender` is a one-line job.
public interface IMediator
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
```

### A2. Dispatcher impl: `Core.Mediation/Mediator.cs` + `Core.Mediation/RequestHandlerWrapper.cs`

Split into two files: `Mediator` → `Mediator.cs`; `RequestHandlerWrapperBase`/`RequestHandlerWrapper<>`/`RequestHandlerWrapperImpl<,>` → `RequestHandlerWrapper.cs`. Both go in the root `...Core.Mediation` namespace and add `using ...Core.Mediation.Abstractions;` (the contracts are in Abstractions).

```csharp
// Mediator.cs
using System.Collections.Concurrent;
using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

namespace NetCoreBackend.NArchitecture.Core.Mediation;

// Runtime dispatcher.
// LIFETIME CONTRACT: Mediator must be registered only as Scoped (or Transient) — NEVER Singleton.
// Because the IServiceProvider injected in the ctor must be bound to a scope to resolve scoped
// handlers (AddScoped). If it is a Singleton, the root provider is captured and scoped handler
// resolution blows up with "Cannot resolve scoped service from root provider." (_wrapperCache is static,
// so the cache is already independent of lifetime — being Scoped does not hurt performance.)
public sealed class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    // A single wrapper instance is cached per request type; MakeGenericType + Activator is expensive.
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapperBase> _wrapperCache = new();

    public Mediator(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Type requestType = request.GetType();
        RequestHandlerWrapperBase wrapper = _wrapperCache.GetOrAdd(requestType, static t =>
        {
            // Extract TResponse from the request type. For a void command, IRequest (non-generic,
            // IsGenericType=false) is skipped, IRequest<Unit> is selected → responseType = Unit.
            Type responseType = t.GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                .GetGenericArguments()[0];

            Type wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(t, responseType);
            return (RequestHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
        });

        return ((RequestHandlerWrapper<TResponse>)wrapper).Handle(request, _serviceProvider, cancellationToken);
    }
}

// ----- RequestHandlerWrapper.cs (separate file) -----
// using Microsoft.Extensions.DependencyInjection;
// using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;
// namespace NetCoreBackend.NArchitecture.Core.Mediation;

// Non-generic base — to hold in the cache.
internal abstract class RequestHandlerWrapperBase { }

// Intermediate layer bound to TResponse — the type Send<TResponse> can cast to.
internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerWrapperBase
{
    public abstract Task<TResponse> Handle(object request, IServiceProvider sp, CancellationToken ct);
}

// Fully typed wrapper — builds and runs the pipeline.
internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> Handle(object request, IServiceProvider sp, CancellationToken ct)
    {
        var handler = sp.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        var behaviors = sp.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();

        // Innermost: the actual handler call.
        RequestHandlerDelegate<TResponse> pipeline = cancellation => handler.Handle((TRequest)request, cancellation);

        // Wrap from the inside out: the first behavior in registration order ends up outermost (guards run first).
        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            IPipelineBehavior<TRequest, TResponse> current = behaviors[i];
            RequestHandlerDelegate<TResponse> next = pipeline;
            pipeline = cancellation => current.Handle((TRequest)request, next, cancellation);
        }

        return pipeline(ct);
    }
}
```

### A3. New file: `Core.Mediation/DependencyInjection/MediatorRegistration.cs`

```csharp
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

namespace NetCoreBackend.NArchitecture.Core.Mediation.DependencyInjection;

// Registration helper that replaces MediatR's AddMediatR(...) call.
public static class MediatorRegistration
{
    public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        // Mediator is stateless; registered as IMediator with scoped lifetime.
        services.AddScoped<IMediator, Mediator>();

        // Only the two-parameter IRequestHandler<,> is scanned. Void handlers also implement this
        // interface (by inheritance) as IRequestHandler<TRequest, Unit>, so they are caught.
        // Nested class handlers (LogoutCommand.LogoutCommandHandler) come through with GetTypes().
        Type handlerInterface = typeof(IRequestHandler<,>);

        foreach (Assembly assembly in assemblies)
        {
            IEnumerable<(Type Service, Type Implementation)> handlerTypes = assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                    .Select(i => (Service: i, Implementation: t)));

            foreach ((Type service, Type implementation) in handlerTypes)
                services.AddScoped(service, implementation);
        }

        return services;
    }
}
```

### A4. `using` change in the 9 behavior files

In each of the files below, replace the `using MediatR;` line with
`using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;`. Do not touch anything else:

1. `Core.Application/DependencyInjection/PipelineBehaviorRegistration.cs`
2. `Core.Application/Pipelines/Authorization/AuthorizationBehavior.cs`
3. `Core.Application/Pipelines/Caching/CacheRemovingBehavior.cs`
4. `Core.Application/Pipelines/Caching/CachingBehavior.cs`
5. `Core.Application/Pipelines/Logging/LoggingBehavior.cs`
6. `Core.Application/Pipelines/Performance/PerformanceBehavior.cs`
7. `Core.Application/Pipelines/Tenancy/TenantValidationBehavior.cs`
8. `Core.Application/Pipelines/Transaction/TransactionScopeBehavior.cs`
9. `Core.Application/Pipelines/Validation/RequestValidationBehavior.cs`

### A5. Test file

In `Core.Test/Application/RequestValidationBehaviorTests.cs`, `using MediatR;` → `using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;`. Also add a ProjectReference to `Core.Mediation` in `Core.Test.csproj` (the test files use the contracts directly; an explicit reference instead of transitive).

### A6. Package/project level — create Core.Mediation, remove MediatR, version/metadata

**Core.Mediation.csproj** (new package):
- `<TargetFramework>net10.0</TargetFramework>`, `ImplicitUsings`/`Nullable` enable, `RootNamespace`/`PackageId` = `NetCoreBackend.NArchitecture.$(AssemblyName)` (repo pattern).
- `<Version>1.0.0</Version>`, `GeneratePackageOnBuild=True`, fill in Title/Description/PackageTags.
- **Single dependency:** `<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />` (the version is already in Directory.Packages.props). An AspNetCore framework ref is NOT NEEDED.
- Add a `README.md` (since Directory.Build.props sets `PackageReadmeFile` unconditionally, pack fails with NU5039 if README is missing).
- `dotnet sln NetCoreBackend.sln add Core.Mediation/Core.Mediation.csproj`.

**Core.Application.csproj**:
- Delete the `<PackageReference Include="MediatR" />` line; in its place add `<ProjectReference Include="..\Core.Mediation\Core.Mediation.csproj" />`.
- "MediatR" → "Mediation" in `<Description>` ("...CQRS, MediatR, Pipelines...") and `<PackageTags>`.
- **SemVer — major bump:** the contracts now come from the `...Core.Mediation.*` namespaces instead of `MediatR` → a **breaking change** in the public API. `<Version>2.0.0</Version>` → **`3.0.0`**.

**Directory.Packages.props**: delete the `<PackageVersion Include="MediatR" Version="14.0.0" />` line.

`Core.Test/Core.Test.csproj` already has `<IsPackable>false</IsPackable>` — the `using` change in the test does not propagate to consumers; no extra package-level action is needed (other than the ProjectReference in A5).

### A7. Verify + publish
- `dotnet build NetCoreBackend.sln` → Core.Mediation, Core.Application and Core.Test must build without errors.
- `dotnet test`, including the A0 spike test, must be green.
- **Publish order matters:** first `Core.Mediation 1.0.0`, then `Core.Application 3.0.0` is packed and pushed to the feed (Core.Application 3.0.0 depends on Core.Mediation 1.0.0).
- ⚠️ **The Version="*" trap:** Side B uses `Version="*"`; NuGet may cache the old version. For a safe transition on Side B: `dotnet nuget locals all --clear` → `dotnet restore`. (More robustly: temporarily pin Side B with `Version="3.0.0"`, verify, then return to `*` if you wish.)

---

## SIDE B — `NetCoreBackendApi/` (consumer API)

> Done after the Side A package is published/restored.
>
> **Note:** Side B has **no** `Directory.Packages.props` (central package management) — package versions are written directly in the csproj files. So on this side there is no MediatR `PackageVersion` line to delete; the only package-level work is the restore/version in B2.

### B0. Pin and restore the new package (BEFORE the find-replace)

Before the find-replace, make sure the consumer pulls the correct package version; otherwise
`*` will serve the old (MediatR-containing) version from the cache and the new namespace will not be found:

- `NetCoreBackendApi.Application.csproj`: temporarily set the `NetCoreBackend.NArchitecture.Core.Application` reference to `Version="3.0.0"` (instead of `*`).
- `dotnet nuget locals all --clear` → `dotnet restore`.
- (After verification is complete, you can revert to `Version="*"` if you wish.)

> Note: At this stage the project **will not build** (the package is at the new namespace but the code still has `using MediatR;`). This is expected; it becomes buildable together with B1+B2.

### B1. Global find-replace (80 files = 77 Features + 3)

In all `.cs` files under `src/`:

```
FIND:    using MediatR;
REPLACE: using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;
```

Full breakdown (verified excluding obj/bin — **80 files**): **77** Features command/query/handler + `Pipelines/ImpersonationAwareAuthorizationBehavior.cs` + `Controllers/BaseController.cs` + `ApplicationServiceRegistration.cs`. Do not touch any other line; the bodies and the `IRequest`/`IRequest<>`/`IRequestHandler<>`/`IRequestHandler<,>`/`IMediator`/`Mediator.Send(...)` usages remain exactly as they are (the types now come from `...Core.Mediation.Abstractions`). After the find-replace, `ApplicationServiceRegistration.cs` gets one additional using in B2.

> **`ImpersonationAwareAuthorizationBehavior.cs` namespace check (no collision):** This file uses the `...Core.Mediation.Abstractions` (IRequest, IPipelineBehavior, RequestHandlerDelegate) + `Pipelines.Authorization` (ISecuredRequest) namespaces together. There is NO type with the same name across these two namespaces; no ambiguity arises. Change only the `using MediatR;` line, leave the second using alone.

### B2. Change the registration line

`src/Application/NetCoreBackendApi.Application/ApplicationServiceRegistration.cs`:

```diff
- services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
+ services.AddMediator(Assembly.GetExecutingAssembly());
```

The `AddMediator` extension is now in the **`NetCoreBackend.NArchitecture.Core.Mediation.DependencyInjection`** namespace. This using does not come from B1's find-replace (that puts `...Mediation.Abstractions`) → add it to this file **by hand**:

```diff
+ using NetCoreBackend.NArchitecture.Core.Mediation.DependencyInjection;
- services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
+ services.AddMediator(Assembly.GetExecutingAssembly());
```

### B3. Verify
- `dotnet build` → the API must build without errors.
- `dotnet test` → the existing tests must pass.
- **Startup verification:** Bring up the API. Since the `AddMediator` scan registers for every `IRequestHandler<,>`, a missing/incorrect handler registration brings the application down on the first request with an `InvalidOperationException` — startup + one request = a registration sanity check.
- **Smoke test — cover EACH of the pipeline behavior and handler types at least once** (77 Send calls, 12 controllers; the following represent every behavior path):
  - **Value-returning query** (two-param handler): `GET /api/Users/{id}` → `GetByIdUserQuery`.
  - **Value-returning command** (two-param handler): `POST /api/Users` → `CreateUserCommand` (returns CreatedUserResponse).
  - **Void command (DIM path — the most critical):** try at least two different ones — `POST .../Logout` (`LogoutCommand`) and `RemoveOrganizationUnitMemberCommand`/`VerifyEmailAuthenticatorCommand`. The side effect (a change in the DB) must occur; if it returns 500, the DIM/registration is broken.
  - **Authorization** (`ISecuredRequest` + Impersonation→Authorization order): 403 with an unauthorized token, 200 with an authorized one; also an **impersonation** scenario (is the SuperAdmin bypass being closed).
  - **Validation** (`RequestValidationBehavior`): 400 + validation messages with an invalid body.
  - **Caching + CacheRemoving:** call a cacheable query twice (the second from cache), then call the related `ICacheRemoverRequest` command, and observe that the query hits the DB again.
  - **Transaction** (`ITransactionalRequest`, e.g. `DeleteOrganizationUnitCommand`/`SyncUserClaimOverridesCommand`): commit on success, error midway → rollback.
  - **Logging** (`ILoggableRequest`): verify a log record is written after a loggable request; for `ISensitiveRequest` (e.g. `LoginCommand`) verify the body is written as `[redacted]`.
  - **Performance** (`IIntervalRequest`): observe that a performance log is written on a request that exceeds the interval (or at least that the behavior runs without error).
  - **TenantValidation** (`ITenantValidationRequest`): without a tenant context (and not as SuperAdmin) `AuthorizationException`/403; 200 with a valid tenant.

> **Out of scope (R4 — TransactionScope async-flow):** `TransactionScopeBehavior` **does not change** in this refactor (only the `using` line). Its async-flow behavior is exactly the same as before; it is not a new risk introduced by this work. It should be addressed separately as existing test debt and does not block this migration.

### B4. Output
Report which files changed, the build/test result, and the smoke test observations. Confirm that no references to MediatR remain with `grep -rn "MediatR" .` (it may only remain inside docs/).

---

## Expected result
- On both sides, no `MediatR` package reference and no `using MediatR;` remain (except in docs).
- All of the API's endpoints behave exactly as before.
- New code: on Side A, **9 files in a separate `Core.Mediation` package** (~180 lines; Abstractions/ 6 + Mediator + RequestHandlerWrapper + DependencyInjection/MediatorRegistration).
- Packages: NEW `Core.Mediation 1.0.0` + `Core.Application 3.0.0`. Publish order: Mediation first, then Application.
- Changed: on Side A, 9+1 `using` + Core.Application.csproj (MediatR→ProjectReference) + props + Core.Test ProjectReference; on Side B, ~80 `using` + 1 additional `.DependencyInjection` using + 1 registration line.
- Side B obtains `Core.Mediation` **transitively** (Core.Application → Core.Mediation); no separate PackageReference is needed.
