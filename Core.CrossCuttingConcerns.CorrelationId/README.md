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

> For DI registration and middleware, use the `Core.CrossCuttingConcerns.CorrelationId.WebApi` package.
