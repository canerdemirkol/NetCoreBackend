# NetCoreBackend.NArchitecture.Core.Mediation

Minimal, license-free in-process mediator for CQRS-style request dispatch and pipeline
behaviors. It is a drop-in replacement for the subset of MediatR the framework uses — no
commercial license, ~150 lines, full control.

## What it provides

| Type | Purpose |
|---|---|
| `IRequest<TResponse>` / `IRequest` | Marker for a request returning a value / a void command |
| `IRequestHandler<TRequest, TResponse>` | Handler for value-returning requests |
| `IRequestHandler<TRequest>` | Handler for void commands (`Task Handle`, bridged to `Unit` via DIM) |
| `IPipelineBehavior<TRequest, TResponse>` | Middleware contract wrapping handler execution |
| `RequestHandlerDelegate<TResponse>` | `next(ct)` delegate passed to behaviors |
| `Unit` | The "no value" return for void commands |
| `IMediator` | Entry point: `Send(request, ct)` |
| `AddMediator(params Assembly[])` | DI registration — scans assemblies for handlers |

## Usage

```csharp
// Composition root
builder.Services.AddMediator(typeof(Program).Assembly);

// Pipeline behaviors (registration order = execution order; first registered runs outermost)
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(MyBehavior<,>));
```

## Design notes

This is **not** a copy of MediatR 14. It deliberately mirrors MediatR 11 semantics
(`IRequest : IRequest<Unit>` plus a Default Interface Method bridge for void handlers) so
that void commands route through a single `Unit` path. Void command handlers must implement
**only** `Task Handle(...)`; the two-parameter `Task<Unit> Handle(...)` is supplied by the DIM
bridge and must not be hand-implemented.

`Mediator` must be registered as **Scoped** (done by `AddMediator`) — never Singleton — so the
injected `IServiceProvider` can resolve scoped handlers.
