# Core.CrossCuttingConcerns.Logging.SeriLog

A Serilog-based implementation of the `ILogger` interface.

## Abstract Class

`SerilogLoggerServiceBase` implements `ILogger` and maps each log level to its Serilog equivalent:

| ILogger | Serilog |
|---|---|
| `Trace` | `Verbose` |
| `Debug` | `Debug` |
| `Information` | `Information` |
| `Warning` | `Warning` |
| `Error` | `Error` |
| `Critical` | `Fatal` |

## Extending

Concrete implementations derive from this class and provide the Serilog sink configuration:

```csharp
public class MyFileLogger : SerilogLoggerServiceBase
{
    public MyFileLogger(FileLogConfiguration config)
        : base(new LoggerConfiguration()
            .WriteTo.File(config.FolderPath)
            .CreateLogger())
    {
    }
}
```

Ready-made implementation: [`Core.CrossCuttingConcerns.Logging.Serilog.File`](../Core.CrossCuttingConcerns.Logging.Serilog.File/README.md)
