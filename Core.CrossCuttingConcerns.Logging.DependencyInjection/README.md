# Core.CrossCuttingConcerns.Logging.DependencyInjection

Logger implementasyonunu DI container'a kaydeden extension metot.

## Kurulum

```csharp
// Program.cs — SerilogFileLogger ile
var fileLogConfig = builder.Configuration
    .GetSection("FileLogConfiguration")
    .Get<FileLogConfiguration>()!;

builder.Services.AddLogging(new SerilogFileLogger(fileLogConfig));
```

`ServiceCollectionLoggingExtensions.AddLogging(ILogger logger)` extension metodu verilen `ILogger` instance'ını singleton olarak kaydeder — tüm request'ler aynı logger instance'ını paylaşır.
