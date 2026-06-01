# Core.CrossCuttingConcerns.Logging.SeriLog

`ILogger` arayüzünün Serilog tabanlı implementasyonu.

## Soyut Sınıf

`SerilogLoggerServiceBase`, `ILogger`'ı implement eder ve her log seviyesini Serilog karşılığına eşler:

| ILogger | Serilog |
|---|---|
| `Trace` | `Verbose` |
| `Debug` | `Debug` |
| `Information` | `Information` |
| `Warning` | `Warning` |
| `Error` | `Error` |
| `Critical` | `Fatal` |

## Genişletme

Somut implementasyonlar bu sınıftan türetilir ve Serilog sink konfigürasyonu sağlar:

```csharp
public class MyFileLogger : SerilogLoggerServiceBase
{
    public MyFileLogger(FileLogConfiguration config)
    {
        Logger = new LoggerConfiguration()
            .WriteTo.File(config.FolderPath)
            .CreateLogger();
    }
}
```

Hazır implementasyon: [`Core.CrossCuttingConcerns.Logging.Serilog.File`](../Core.CrossCuttingConcerns.Logging.Serilog.File/README.md)
