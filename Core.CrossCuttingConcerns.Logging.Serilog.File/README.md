# Core.CrossCuttingConcerns.Logging.Serilog.File

Günlük rolling file sink'leri ile dosya tabanlı Serilog logger implementasyonu.

## Özellikler

- Günlük log rotasyonu (daily rolling)
- 50 MB dosya boyutu sınırı
- Genel log dosyası (`AllLogs.txt`)
- Servis bazlı ayrı klasörler (`Logs/UserService/...`)
- HTTP request log dosyası (`HttpLog.txt`)

## Kurulum

```csharp
// DI kaydı için Core.CrossCuttingConcerns.Logging.DependencyInjection kullanın
builder.Services.AddSingleton<ILogger, SerilogFileLogger>(sp =>
    new SerilogFileLogger(config.GetSection("FileLogConfiguration").Get<FileLogConfiguration>()!));
```

## appsettings.json

```json
{
  "FileLogConfiguration": {
    "FolderPath": "Logs",
    "MinimumLogEventLevel": "Information",
    "OutputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
    "SpecificFolderPaths": {
      "UserService": "Logs/UserService",
      "OrderService": "Logs/OrderService"
    }
  }
}
```

## Oluşturulan Dosyalar

```
Logs/
├── AllLogs-2026-06-01.txt
├── HttpLog-2026-06-01.txt
├── UserService/
│   └── UserService-2026-06-01.txt
└── OrderService/
    └── OrderService-2026-06-01.txt
```
