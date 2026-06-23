# Core.CrossCuttingConcerns.CorrelationId.WebApi

ASP.NET Core middleware and extensions: reads the incoming `X-Correlation-Id` header, falls back to the active distributed trace ID if absent, and generates a new GUID if neither is available. It propagates the value as a response header and automatically enriches the Serilog LogContext.

## Installation

```
dotnet add package NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.WebApi
```

## Program.cs

```csharp
// 1. DI registration
builder.Services.AddCorrelationId();

// 2. Middleware — add at the start of the pipeline, before the exception middleware
app.UseCorrelationId();
app.ConfigureCustomExceptionMiddleware(); // exception logs also carry the CorrelationId
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

## appsettings.json — Serilog template

For `{CorrelationId}` to appear in log lines, users of `Core.CrossCuttingConcerns.Logging.Serilog.File` should add it to the template:

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

## Access Points

| Location | How |
|---|---|
| Controller / Service (DI) | `ICorrelationIdAccessor.CorrelationId` |
| HttpContext | `httpContext.GetCorrelationId()` |
| Response header | `X-Correlation-Id` |
| Serilog log lines | Automatic — `{CorrelationId}` template property |

## ID Priority Order

1. Incoming `X-Correlation-Id` request header (API gateway / service mesh propagation)
2. `Activity.Current?.TraceId` — aligns with the trace when OpenTelemetry or Application Insights is active
3. `Guid.NewGuid()` — standalone fallback
