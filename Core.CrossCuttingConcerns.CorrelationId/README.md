# Core.CrossCuttingConcerns.CorrelationId

Her request'i takip edebilmek için correlation ID abstraction'ı. Application katmanında `ICorrelationIdAccessor`'a bağımlı olunabilir; ASP.NET Core dependency'si gerekmez.

## Kurulum

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

## Kullanım (Application / MediatR Handler)

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
        // audit log, outbox mesajı vb. için kullanılabilir
    }
}
```

> DI kaydı ve middleware için `Core.CrossCuttingConcerns.CorrelationId.WebApi` paketini kullanın.
