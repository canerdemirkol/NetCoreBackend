# Core.CrossCuttingConcerns.Logging.Abstraction

The logger interface. The contract that all logging implementations must comply with.

## Interface

```csharp
public interface ILogger
{
    void Trace(string message);
    void Debug(string message);
    void Information(string message);
    void Warning(string message);
    void Error(string message);
    void Critical(string message);
}
```

## Usage

```csharp
// DI inject
public class MyService
{
    private readonly ILogger _logger;

    public MyService(ILogger logger) => _logger = logger;

    public void DoWork()
    {
        _logger.Information($"[{nameof(MyService)}.{nameof(DoWork)}] executed by system");
    }
}
```

Implementations: [`Core.CrossCuttingConcerns.Logging.SeriLog`](../Core.CrossCuttingConcerns.Logging.SeriLog/README.md)
