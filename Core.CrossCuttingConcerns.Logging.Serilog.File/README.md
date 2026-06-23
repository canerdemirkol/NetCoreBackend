# Core.CrossCuttingConcerns.Logging.Serilog.File

A file-based Serilog logger implementation with daily rolling file sinks.

## Features

- Daily log rotation (daily rolling)
- 50 MB file size limit
- General log file (`AllLogs.txt`)
- Separate per-service folders (`SpecificLogFolders`)
- HTTP request log file (`HttpLog.txt`)
- `LogContext` enrichment support — ambient properties such as `{CorrelationId}` are automatically added to log lines

## Installation

```csharp
// Program.cs
builder.Services.AddSingleton<ILogger, SerilogFileLogger>(sp =>
    new SerilogFileLogger(
        builder.Configuration.GetSection("FileLogConfiguration").Get<FileLogConfiguration>()!));
```

## appsettings.json

```json
{
  "FileLogConfiguration": {
    "FolderPath": "Logs",
    "MinLogLevel": "Information",
    "LogOutputTemplate": "[{Timestamp:dd.MM.yyyy HH:mm:ss}] [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
    "SpecificLogFolders": ["UserService", "OrderService"]
  }
}
```

> For `{CorrelationId}` to work, the `Core.CrossCuttingConcerns.CorrelationId.WebApi` package must be added to the pipeline via `app.UseCorrelationId()`.

## Generated Files

```
Logs/
├── GeneralLogs/
│   └── AllLogs.txt
├── HttpLogs/
│   └── HttpLog.txt
├── UserService/
│   └── UserService.txt
└── OrderService/
    └── OrderService.txt
```

## Changelog

### 1.0.1
- Added `Enrich.FromLogContext()` — properties set via `LogContext.PushProperty` are now reflected in log lines
