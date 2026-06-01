# Core.CrossCuttingConcerns.Logging.Abstraction

Logger arayüzü. Tüm loglama implementasyonlarının uyması gereken contract.

## Interface

```csharp
public interface ILogger
{
    void Trace(LogDetail logDetail);
    void Debug(LogDetail logDetail);
    void Information(LogDetail logDetail);
    void Warning(LogDetail logDetail);
    void Error(LogDetailWithException logDetail);
    void Critical(LogDetailWithException logDetail);
}
```

## Kullanım

```csharp
// DI inject
public class MyService
{
    private readonly ILogger _logger;

    public MyService(ILogger logger) => _logger = logger;

    public void DoWork()
    {
        _logger.Information(new LogDetail
        {
            FullName = nameof(MyService),
            MethodName = nameof(DoWork),
            User = "system"
        });
    }
}
```

Implementasyonlar: [`Core.CrossCuttingConcerns.Logging.SeriLog`](../Core.CrossCuttingConcerns.Logging.SeriLog/README.md)
