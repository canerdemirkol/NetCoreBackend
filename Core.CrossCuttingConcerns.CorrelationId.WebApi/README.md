# Core.CrossCuttingConcerns.CorrelationId.WebApi

ASP.NET Core middleware ve extension'ları: gelen `X-Correlation-Id` header'ını okur, yoksa aktif distributed trace ID'sini kullanır, o da yoksa yeni bir GUID üretir. Response header olarak yayar ve Serilog LogContext'i otomatik zenginleştirir.

## Kurulum

```
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.WebApi
```

## Program.cs

```csharp
// 1. DI kaydı
builder.Services.AddCorrelationId();

// 2. Middleware — pipeline'ın başına, exception middleware'inden önce ekleyin
app.UseCorrelationId();
app.ConfigureCustomExceptionMiddleware(); // exception log'ları da CorrelationId taşır
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

## appsettings.json — Serilog template

`{CorrelationId}` log satırlarında görünmesi için `Core.CrossCuttingConcerns.Logging.Serilog.File` kullananlar template'e eklesin:

```json
{
  "FileLogConfiguration": {
    "FolderPath": "Logs",
    "MinLogLevel": "Information",
    "LogOutputTemplate": "[{Timestamp:dd.MM.yyyy HH:mm:ss}] [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
    "SpecificLogFolders": []
  }
}
```

## Erişim Noktaları

| Yer | Nasıl |
|---|---|
| Controller / Service (DI) | `ICorrelationIdAccessor.CorrelationId` |
| HttpContext | `httpContext.GetCorrelationId()` |
| Response header | `X-Correlation-Id` |
| Serilog log satırları | Otomatik — `{CorrelationId}` template property |

## ID Öncelik Sırası

1. Gelen `X-Correlation-Id` request header'ı (API gateway / service mesh propagation)
2. `Activity.Current?.TraceId` — OpenTelemetry veya Application Insights aktifse trace ile örtüşür
3. `Guid.NewGuid()` — standalone fallback
