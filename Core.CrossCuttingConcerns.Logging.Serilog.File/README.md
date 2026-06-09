# Core.CrossCuttingConcerns.Logging.Serilog.File

Günlük rolling file sink'leri ile dosya tabanlı Serilog logger implementasyonu.

## Özellikler

- Günlük log rotasyonu (daily rolling)
- 50 MB dosya boyutu sınırı
- Genel log dosyası (`AllLogs.txt`)
- Servis bazlı ayrı klasörler (`SpecificLogFolders`)
- HTTP request log dosyası (`HttpLog.txt`)
- `LogContext` enrichment desteği — `{CorrelationId}` gibi ambient property'ler otomatik log satırlarına eklenir

## Kurulum

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

> `{CorrelationId}` çalışması için `Core.CrossCuttingConcerns.CorrelationId.WebApi` paketinin `app.UseCorrelationId()` ile pipeline'a eklenmiş olması gerekir.

## Oluşturulan Dosyalar

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

## Değişiklik Geçmişi

### 1.0.1
- `Enrich.FromLogContext()` eklendi — `LogContext.PushProperty` ile set edilen property'ler artık log satırlarına yansır
