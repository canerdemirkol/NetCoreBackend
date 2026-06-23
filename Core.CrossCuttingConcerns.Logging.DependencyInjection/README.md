# Core.CrossCuttingConcerns.Logging.DependencyInjection

An extension method that registers the logger implementation with the DI container.

## Installation

```csharp
// Program.cs — with SerilogFileLogger
var fileLogConfig = builder.Configuration
    .GetSection("FileLogConfiguration")
    .Get<FileLogConfiguration>()!;

builder.Services.AddLogging(new SerilogFileLogger(fileLogConfig));
```

The `ServiceCollectionLoggingExtensions.AddLogging(ILogger logger)` extension method registers the given `ILogger` instance as a singleton — all requests share the same logger instance.
