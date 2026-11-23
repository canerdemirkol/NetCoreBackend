# Core.CrossCuttingConcerns.CorrelationId

A correlation ID abstraction for tracking each request. The application layer can depend on `ICorrelationIdAccessor` without requiring an ASP.NET Core dependency.

## Installation

```
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId
```

## ICorrelationIdAccessor

```csharp
public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
}
```

## Usage (Application / MediatR Handler)

```csharp
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand>
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    public CreateOrderCommandHandler(ICorrelationIdAccessor correlationIdAccessor)
    {
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        string? correlationId = _correlationIdAccessor.CorrelationId;
        // can be used for audit logs, outbox messages, etc.
    }
}
```

## Background work (1.1.0)

A scheduled job's run has no HTTP request to carry an id. `BackgroundCorrelation.Begin(job)` opens one (`job-{job}-{guid}`) for the run;
the WebApi package's accessor returns it when there is no request, so everything written in that run carries the same id. It flows with the
async context: parallel runs never share one, and a nested run restores the outer id when disposed.

```csharp
using (BackgroundCorrelation.Begin("payments"))
{
    await ReconcileAsync(cancellationToken); // logs and audit rows written here carry the run's id
}
```

> For DI registration and middleware, use the `Core.CrossCuttingConcerns.CorrelationId.WebApi` package.
