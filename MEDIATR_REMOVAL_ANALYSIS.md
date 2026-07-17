# MediatR Dependency Removal Analysis

> **✅ IMPLEMENTED (2026-06-23):** Side A (`Core.Mediation 1.0.0` + `Core.Application 3.0.0`, published)
> and Side B (NetCoreBackendApi) are complete and verified. See CHANGELOG.md. Historical record.

> **Context:** MediatR 14.0.0 ships as a transitive dependency of the NArchitecture Core package. As of MediatR 12.4.0, a commercial license is mandatory. This analysis maps out every use of MediatR in the project and evaluates the option of removing the package and writing our own minimal implementation.

---

## 0. THERE ARE TWO SEPARATE SIDES — Read This First (for the AI implementer)

This work spans **two separate solutions/repos**. Not confusing them is critical:

| | **Side A — Library** | **Side B — Consumer API** |
|---|---|---|
| Repo/folder | `NetCoreBackend/` | `NetCoreBackendApi/` |
| Project inside | **`Core.Application`** + **NEW `Core.Mediation`** (+ other Core.* projects) | `NetCoreBackendApi.Application`, `.WebAPI`, `.Persistence` … |
| MediatR `PackageReference` | **Here** (`Core.Application.csproj`) — the only place | None; it pulls the package transitively |
| Pipeline behaviors | Here (`Core.Application/Pipelines/*`) | None (only its own `ImpersonationAwareAuthorizationBehavior`) |
| How it gets MediatR | **Produces** the NuGet package | **Consumes** the `NetCoreBackend.NArchitecture.Core.Application` package (`Version="*"`) |

**In other words: every "Core.Application" in this document = Side A (NetCoreBackend). On the API side, there is NO project named `Core.Application`.**

**Workflow (in order):**

```
SIDE A — NetCoreBackend                            SIDE B — NetCoreBackendApi (API)
(library, source of the package)                   (consumer)
─────────────────────────────────────             ─────────────────────────────────────
1. NEW project: Core.Mediation                     3. Move to the new package versions:
   • Abstractions/ (IRequest, IRequest<>,  ─pkg──►     • using MediatR; →
     Unit, IRequestHandler<>,              (NuGet)        using ...Core.Mediation.Abstractions; (80 files)
     IRequestHandler<,>, IPipelineBehavior<,>,         • addition to ApplicationServiceRegistration.cs:
     RequestHandlerDelegate, IMediator)                   using ...Core.Mediation.DependencyInjection;
   • Mediator + RequestHandlerWrapper (impl)         • AddMediatR(...) → AddMediator(...)
   • DependencyInjection/MediatorRegistration
2. Core.Application:
   • Remove the MediatR PackageReference
   • Add a ProjectReference to Core.Mediation
   • using change in 9 behavior files
```

> **ARCHITECTURAL DECISION (made during implementation):** Rather than being embedded *inside*
> Core.Application, the mediation code was extracted into a **separate `Core.Mediation` NuGet package**.
> Rationale: the repo already organizes every concern as a separate package + separate
> `.Abstraction`/`.DependencyInjection` namespace pattern (like Core.MultiTenancy, Core.Outbox). Hence
> there are two namespaces: `...Core.Mediation.Abstractions` (contracts) and
> `...Core.Mediation.DependencyInjection` (AddMediator). The impl (`Mediator`) lives in the root
> `...Core.Mediation` namespace.

In the sections below, "Side A" = `NetCoreBackend` (Core.Mediation + Core.Application), and "this API (NetCoreBackendApi)" = Side B.

---

## 1. Current State: Where Is MediatR Used?

### Package Dependency

There is a `MediatR` package reference in **a single place** (Side A — NetCoreBackend):

- `NetCoreBackend/Core.Application/Core.Application.csproj` → `<PackageReference Include="MediatR" />`
- `NetCoreBackend/Directory.Packages.props` → `MediatR 14.0.0`

The other Core.* projects in NetCoreBackend do not depend on MediatR (a `using MediatR` scan across the entire solution returned only Core.Application's 9 behavior files + 1 test). So on the library side, the **dependency lives on the surface of a single package**. This makes the write-your-own option extremely attractive.

### MediatR APIs Used in Code

11 files inside `Core.Application` contain `using MediatR;`. All symbols used:

| MediatR Symbol | Where It Is Used | What It Does |
|---|---|---|
| `IRequest<TResponse>` | 8 pipelines + 1 test | Marker interface — "this class is a request" |
| `IPipelineBehavior<TRequest, TResponse>` | 8 pipeline behaviors | Behavior contract for the pipeline (middleware) chain |
| `RequestHandlerDelegate<TResponse>` | 8 pipeline behaviors | The `next(ct)` delegate — moves to the next step in the pipeline |
| `AddMediatR(...)` / `RegisterServicesFromAssembly(...)` | Only in SETUP.md and README.md (as examples) | Registers handlers into DI in consumer applications |

### Unused MediatR Features

These features are **never used in the project** — we do not have to implement them when writing our own:

- ❌ `INotification` / `INotificationHandler` — publish-subscribe (event broadcasting). A search for `INotification`/`IPublisher`/`.Publish(` across the entire solution returned zero results.
- ❌ `IStreamRequest` / streaming
- ❌ `IRequestPreProcessor` / `IRequestPostProcessor` — preprocessor/postprocessor (pipeline behaviors already do this)
- ❌ `RequestExceptionHandler` / `RequestExceptionAction`

### APIs that ARE USED but were MISSED in the first draft of this analysis (Critical Correction)

> Because the first draft looked only inside the `Core.Application` package, it missed how the consumer API (`NetCoreBackendApi`) uses MediatR. The following are **heavily used** and **must be present** in our own implementation, otherwise the API will not compile:

- ✅ **`IMediator`** — `BaseController.cs:11` `IMediator Mediator => HttpContext.RequestServices.GetService<IMediator>()!`. All controllers use this to send requests (`Mediator.Send(...)`). `ISender` is never used in the consumer; the type used is `IMediator`.
- ✅ **Non-generic `IRequest`** — **11 void commands** implement this (`LogoutCommand`, `VerifyEmailAuthenticatorCommand`, `DeleteOrganizationUnitCommand`, `SyncRoleClaimsCommand`, …). In MediatR, `IRequest : IRequest<Unit>`.
- ✅ **Single-parameter `IRequestHandler<TRequest>`** — **11 void handlers** implement this; the body returns `Task Handle(...)` (not `Task<TResponse>`) (`LogoutCommand`, …). In MediatR, `IRequestHandler<TRequest> : IRequestHandler<TRequest, Unit>`.
- ✅ **`Unit`** — MANDATORY for bridging void commands/handlers across the above `IRequest`→`IRequest<Unit>` and `Task`→`Task<Unit>`. (The first draft flagged this as "not needed" — that was wrong.)
- ✅ **Two-parameter `IRequestHandler<TRequest, TResponse>`** — all query/command handlers that return a value (`DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, DeletedUserResponse>`, …).

**Conclusion:** You use a small slice of MediatR's surface area, but the slice you use is **wider** than the first draft assumed — particularly the void-command (`Unit`) path and the `IMediator` entry point.

---

## 2. Our Own Implementation — Detailed Design

All we need is **3 things**: marker/handler interfaces, the pipeline behavior contract, and a runtime dispatcher. All of these live in a separate **`Core.Mediation`** package (the code blocks below show the namespaces accordingly). For the physical file layout, see §3 — each type in its own file as a best practice.

### 2.1 Core Abstractions (≈ 20 lines) — `Core.Mediation/Abstractions/`

```csharp
namespace NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

// "This class is a request returning TResponse" — a marker, no behavior.
public interface IRequest<out TResponse> { }

// Non-generic marker — for void commands. In MediatR, IRequest : IRequest<Unit>.
// 11 void commands (LogoutCommand, VerifyEmailAuthenticatorCommand, ...) use this.
public interface IRequest : IRequest<Unit> { }

// "A return with no value" — the TResponse of void commands. A struct because it has a single value.
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

// Two-parameter handler — query/command handlers that return a value (exact MediatR signature).
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

// Single-parameter handler — void command handlers. The body writes `Task Handle(...)`.
// A Default Interface Method (DIM) bridges Task → Task<Unit>; the dispatcher always
// resolves and calls the two-parameter IRequestHandler<TRequest, Unit>, and the DIM steps in.
// C# 8+ DIM feature — valid and compiles on net10.0.
//
// PRECONDITION: void handler classes must ONLY write `Task Handle(...)`; they must NOT
// implement the two-parameter `Task<Unit> Handle(...)` themselves. Otherwise the DIM is disabled.
// All 11 existing void handlers satisfy this condition.
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

// Pipeline behavior contract — exactly the same signature as MediatR.
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}

// The entry point that controllers inject (BaseController does GetService<IMediator>()).
// NOTE: MediatR has a separate ISender; since nothing in this project uses ISender,
// we did not add it per YAGNI (the implementation prompt has no ISender either). It can be
// split out in one line if needed.
public interface IMediator
{
    Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
```

> ⚠️ **CAUTION (for the implementing AI) — this is NOT a COPY of MediatR 14, it is our own design.**
> These signatures are compatible with the **usage** in the project (consumer code compiles without
> any change), but they are **not identical** to MediatR 14's actual type definitions. Deliberate differences:
> - MediatR 14: `IRequest : IBaseRequest` (does not inherit Unit). **Us:** `IRequest : IRequest<Unit>` (MediatR 11 semantics) — to perform void dispatch through a single `Unit` path.
> - MediatR 14: the single-param `IRequestHandler<T>` is an independent interface, no DIM. **Us:** we bind it to `IRequestHandler<T, Unit>` via a DIM bridge.
> - MediatR 14: the `IPipelineBehavior` constraint is `where TRequest : notnull`. **Us:** `where TRequest : IRequest<TResponse>` (existing behaviors already satisfy this).
>
> **Do not look at the MediatR source and "fix" this design.** The design is internally consistent and works once tested.

**Migration impact:** in each of the 9 pipeline behavior files (Core.Application + the consumer's `ImpersonationAwareAuthorizationBehavior`), just `using MediatR;` → `using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;`. The consumer's command/handler/controller files also see only this `using` change; the bodies do not change. (Only `ApplicationServiceRegistration.cs` additionally requires `using ...Core.Mediation.DependencyInjection;` for `AddMediator`.)

**Why `IMediator` and `Unit` are required:** `BaseController` does `GetService<IMediator>()`; void commands use `IRequest` (= `IRequest<Unit>`) and `IRequestHandler<TRequest>` (`Task Handle`). Without these you get a **compile error** — this was the biggest gap in the first draft.

### 2.2 Mediator (Dispatcher) Implementation

This is where the real weight of the work lies. We need to solve three problems:

1. **Runtime type resolution:** `IMediator.Send` is called with `IRequest<TResponse>` — the concrete `TRequest` type is learned at runtime.
2. **Pipeline composition:** take all `IPipelineBehavior<,>` instances from DI and build the nested delegate chain so that the innermost call is `IRequestHandler<,>.Handle`.
3. **Type caching:** calling `MakeGenericType` every time for the same request type is slow — cache it with a `ConcurrentDictionary`.

```csharp
// Runtime dispatcher. LIFETIME: must be registered Scoped (or Transient) — NEVER Singleton.
// The IServiceProvider injected in the ctor must be bound to a scope to resolve scoped handlers;
// a Singleton captures the root provider and scoped handler resolution blows up. _wrapperCache is static,
// so the cache is independent of lifetime.
public sealed class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    // One wrapper instance is cached per request type.
    // MakeGenericType + Activator.CreateInstance is expensive — we do not want to pay it per request.
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapperBase> _wrapperCache = new();

    public Mediator(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Type requestType = request.GetType();
        RequestHandlerWrapperBase wrapper = _wrapperCache.GetOrAdd(requestType, static t =>
        {
            // We extract the TResponse generic argument from the request type:
            // MyQuery : IRequest<MyDto> → genericArguments[0] = MyDto
            Type responseType = t.GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                .GetGenericArguments()[0];

            Type wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(t, responseType);
            return (RequestHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
        });

        return ((RequestHandlerWrapper<TResponse>)wrapper)
            .Handle(request, _serviceProvider, cancellationToken);
    }
}

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
        RequestHandlerDelegate<TResponse> pipeline =
            cancellation => handler.Handle((TRequest)request, cancellation);

        // Wrap from the inside out: the first behavior in registration order ends up outermost.
        // PipelineBehaviorRegistration.cs has the order Authorization → Validation → Caching...,
        // and this ordering must be preserved (Authorization must run first).
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

**Algorithmic complexity:**

- `Send` on a cache hit: **O(1)** — just a dictionary lookup + a few delegate allocations
- On a cache miss: **O(k)** (k = number of behaviors, 8 in the project), one-time per request type
- Memory: **a single wrapper instance** per request type + **k+1 delegate closures** per call (unavoidable, this is what MediatR does too)

### 2.3 DI Registration

```csharp
public static class MediatorRegistration
{
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        // Mediator is stateless; registered as IMediator with scoped lifetime.
        services.AddScoped<IMediator, Mediator>();

        // Only the two-parameter IRequestHandler<,> is scanned. Void handlers also implement
        // this interface (by inheritance) as IRequestHandler<TRequest, Unit>, so they are
        // caught here — there is no need to additionally register the single-parameter interface.
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

Usage in the consumer application — **drops in as a one-for-one replacement of the existing `AddMediatR` call**:

```csharp
// Before: builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
// After:
builder.Services.AddMediator(typeof(Program).Assembly);
```

`PipelineBehaviorRegistration.cs` already registers manually with `services.AddScoped(typeof(IPipelineBehavior<,>), typeof(...))` — this code **does not change at all**, only `using MediatR;` → `using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;`.

> **Note:** `AddMediator` and `MediatorRegistration` are in the `...Core.Mediation.DependencyInjection` namespace; `Mediator` and the wrappers are in the root `...Core.Mediation`; the contracts (`IRequest`, `IMediator`, `Unit`, …) are in `...Core.Mediation.Abstractions`. In most files the consumer sees only `.Abstractions`; the only file that calls `AddMediator` (`ApplicationServiceRegistration.cs`) additionally imports `.DependencyInjection`.

---

## 3. Migration Impact — How Many Files Change?

### SIDE A — `NetCoreBackend` (library)

#### A-1) NEW project: `Core.Mediation` (9 new files)

All mediation code in a separate package. Each type in its own file as a best practice; generic
arity-overload pairs (`IRequest`/`IRequest<>`, `IRequestHandler`/`IRequestHandler<,>`) together.

```
Core.Mediation/
├── Abstractions/                    → namespace ...Core.Mediation.Abstractions
│   ├── IRequest.cs                    (IRequest<TResponse> + IRequest)
│   ├── IRequestHandler.cs             (IRequestHandler<,> + IRequestHandler<> DIM bridge)
│   ├── IPipelineBehavior.cs
│   ├── RequestHandlerDelegate.cs
│   ├── Unit.cs
│   └── IMediator.cs
├── Mediator.cs                      → namespace ...Core.Mediation (dispatcher)
├── RequestHandlerWrapper.cs         → namespace ...Core.Mediation (internal wrappers)
└── DependencyInjection/             → namespace ...Core.Mediation.DependencyInjection
    └── MediatorRegistration.cs        (AddMediator)
```

`Core.Mediation.csproj`: only a `Microsoft.Extensions.DependencyInjection.Abstractions`
reference (minimal surface). `Version=1.0.0`. Added to the solution.

#### A-2) `Core.Application` — changed files

In 9 behavior files + 1 test file, **a single line**: the namespace import.

```diff
- using MediatR;
+ using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;
```

1. `PipelineBehaviorRegistration.cs`
2. `Pipelines/Tenancy/TenantValidationBehavior.cs`
3. `Pipelines/Validation/RequestValidationBehavior.cs`
4. `Pipelines/Caching/CachingBehavior.cs`
5. `Pipelines/Caching/CacheRemovingBehavior.cs`
6. `Pipelines/Authorization/AuthorizationBehavior.cs`
7. `Pipelines/Transaction/TransactionScopeBehavior.cs`
8. `Pipelines/Performance/PerformanceBehavior.cs`
9. `Pipelines/Logging/LoggingBehavior.cs`
10. `Core.Test/Application/RequestValidationBehaviorTests.cs` (test)

### SIDE A — package level (`NetCoreBackend`)

- **NEW package:** `NetCoreBackend.NArchitecture.Core.Mediation 1.0.0` is produced.
- The `MediatR 14.0.0` line in `Directory.Packages.props` is deleted.
- In `Core.Application.csproj`, `<PackageReference Include="MediatR" />` is deleted; in its place
  `<ProjectReference Include="..\Core.Mediation\Core.Mediation.csproj" />` is added.
  "MediatR" → "Mediation" in `<Description>`/`<PackageTags>`.
- **SemVer:** the namespace change is a breaking change in the public API → `Core.Application` version
  `2.0.0` → **`3.0.0`** (major bump). `Core.Mediation` is a new package → `1.0.0`.
- Consumers now obtain `IRequest`, `IRequestHandler`, `IMediator`, `Unit` transitively through the
  `Core.Application` → `Core.Mediation` chain (under the new namespaces).
- **Publish order:** first `Core.Mediation 1.0.0`, then `Core.Application 3.0.0`
  (because Core.Application references it).

### SIDE B — This API (`NetCoreBackendApi`), the real work SKIPPED as the consumer

> The first draft said "NOTHING CHANGES except Core.Application." **This is wrong.** This repo consumes MediatR directly and **has `using MediatR;` in 80 files**. All of them see a namespace change:

```diff
- using MediatR;
+ using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;
```

Breakdown — **exactly 80 files** (verified with `grep -rl "using MediatR;" src` excluding obj/bin; 77 + 3 = 80):
- **77** command/query/handler files under Features (`IRequest<>`, `IRequest`, `IRequestHandler<,>`, `IRequestHandler<>`)
- **1** `Pipelines/ImpersonationAwareAuthorizationBehavior.cs` (the consumer's own pipeline behavior)
- **1** `Controllers/BaseController.cs` (`IMediator`)
- **1** `ApplicationServiceRegistration.cs` — **THREE** changes here:
  1. `using MediatR;` → `using ...Core.Mediation.Abstractions;`
  2. **Additional using:** `using NetCoreBackend.NArchitecture.Core.Mediation.DependencyInjection;` (AddMediator is here)
  3. `services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()))` → `services.AddMediator(Assembly.GetExecutingAssembly())`

The remaining controllers (`UsersController`, etc.) do not contain `using MediatR;` — they inherit the `Mediator` property from `BaseController` and are left untouched.

This is a mechanical find-replace; behavior does not change. But it is not "zero files," it is **80 files + 1 additional `using` (the registration file) + 1 registration line**. (There is no `Directory.Packages.props` on Side B; the csproj version is pinned in B0.)

---

## 4. Risk and Tradeoff Assessment

### Advantages

| Advantage | Description |
|---|---|
| **No license** | MediatR 12.4+ commercial license — requires an annual fee or an OSS exemption |
| **Full control** | Debug, customize, optimize — it is your own code |
| **Smaller surface** | MediatR is ~5000 LOC, your need is ~150 LOC. Easier to maintain and document |
| **No version lock** | You do not have to track MediatR's breaking changes |
| **Fewer dependencies** | The NArchitecture packages become "license clean" — easier to distribute to others |

### Disadvantages / Risks

| Risk | Impact | Mitigation |
|---|---|---|
| **Maintenance burden** | A piece of runtime code under your ownership | Low — 150 lines of code, rarely changes |
| **MediatR's hidden optimizations are absent** | Cached compiled expressions, hot-path tuning | Low — your usage profile is CRUD-heavy, the throughput limit is the DB, not MediatR |
| **What if NotificationPublisher is needed?** | If in-process event broadcasting is needed later, you would have to write it yourself | No need right now; ~50 more lines added when needed |
| **3rd-party ecosystem** | MediatR.Extensions.* packages will not work | You do not use any related package — I checked |
| **AOT/trimming scenario** | `MakeGenericType` + reflection is AOT-unfriendly | MediatR has this too — no difference |
| **Untested code** | New code, new bug risk | A dispatcher test suite must be written, like in your RequestValidationBehavior test |

### Critical Detail: Pipeline Ordering Behavior

In MediatR, the `IPipelineBehavior<,>` registration order corresponds to the **execution order** (the first registered is outermost). Our wrapper code guarantees this (`for i = length-1 downTo 0`), but this contract **must be documented** — `PipelineBehaviorRegistration.cs:34-41` already depends on this order.

---

## 5. Tests to Write While Doing This

Minimum test set (following the existing `RequestValidationBehaviorTests` pattern):

1. **`Mediator_Routes_Request_To_Correct_Handler`** — does it find and call the correct handler registered in DI
2. **`Mediator_Throws_When_Handler_Not_Registered`** — a clear error on a missing handler
3. **`Pipeline_Behaviors_Run_In_Registration_Order`** — do the guard behaviors (auth, validation) run before the side-effect behaviors (cache, transaction)
4. **`Pipeline_Behavior_Can_Short_Circuit`** — when a behavior `throw`s without calling `next()`, is the handler not called (auth/validation scenario)
5. **`Pipeline_Behavior_Exception_Propagates`** — does an exception thrown from the handler or a behavior reach the caller
6. **`Cancellation_Token_Flows_End_To_End`** — is the CT passed unchanged from the dispatch point to the handler
7. **`Concurrent_Send_Calls_Are_Isolated`** — the wrapper cache is thread-safe (same pattern as the parallel-stress in your existing test)
8. **`Generic_Type_Cache_Reuses_Wrapper`** — a cache hit for the same request type, `MakeGenericType` is not called on the second `Send`
9. **`Void_Command_Routes_To_Single_Param_Handler`** — the `IRequest` (non-generic) + `IRequestHandler<TRequest>` (`Task Handle`) path: does the dispatcher resolve `IRequestHandler<TRequest, Unit>` and run the DIM bridge, and does the handler's side effect occur (the most critical correction — without this path, the 11 void commands do not compile/run)
10. **`Mediator_Resolvable_As_IMediator`** — does `GetService<IMediator>()` work like in `BaseController`

These ~10 tests are ~250 lines of xUnit code — an afternoon's work.

## 6. A Few Questions to Help You Decide

This analysis answers "is it feasible?" with a **clear: yes**. On the decision side:

1. **Is the license a real blocker?** If Obase is building a commercial product and does not want to buy a MediatR Commercial License, this investment pays for itself. If it is your own internal project and an OSS exemption is sufficient, there is not enough reason to drop MediatR.

2. **Will a NotificationPublisher / event broadcasting need arise soon?** If the Outbox pattern (I see it in Core.Outbox) is going to be extended with in-process event broadcasting, you will also need to write MediatR's `INotification` mechanism (+~80 lines).

3. **Who will bear the burden of carrying the breaking change?** The `using MediatR;` and `AddMediatR(...)` lines in consumer applications — are they under your control, or will other teams touch them?

---

## Summary

- **Total new code:** ~180 lines, **9 files in a separate `Core.Mediation` package** (Abstractions/: 6 contract files; root: Mediator + RequestHandlerWrapper; DependencyInjection/: MediatorRegistration) + ~250 lines of tests
- **Changed existing code (Core.Application):** a `using` line in 9 behaviors + 1 test file + the MediatR PackageReference → Core.Mediation ProjectReference in the csproj + deleting the MediatR line in props
- **Changed existing code (this API):** a `using` line in **80 files** (77 Features + BaseController + ImpersonationAware + ApplicationServiceRegistration) + the additional `.DependencyInjection` using in `ApplicationServiceRegistration.cs` + `AddMediatR` → `AddMediator`
- **Packages:** NEW `Core.Mediation 1.0.0` + `Core.Application 2.0.0 → 3.0.0`. Publish order: Mediation first, then Application.
- **Behavior change:** Zero — all of MediatR's semantics used in the project are preserved
- **Lost MediatR features:** `INotification`, streaming, pre/post-processor — none of which are used at present

### The 4 critical points corrected versus the first draft

1. **`IMediator` added** — `BaseController` injects `IMediator`, not `ISender`. Without it, it does not compile.
2. **Non-generic `IRequest` added** — 11 void commands use it.
3. **Single-parameter `IRequestHandler<TRequest>` (DIM bridge) added** — 11 void handlers use it.
4. **The "not needed" claim about `Unit` corrected** — it is mandatory for the void path.

With these corrections: **Yes, once the API uses the new packages, it sheds the MediatR dependency and preserves its functionality one-for-one.** Had it been implemented as in the first draft, ~22 files (11 void commands + 11 void handlers) + all controllers would not have compiled. The real decision is in the license/maintenance tradeoff — the code side, with these corrections, is not a blocker.
