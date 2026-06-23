# Core.CrossCuttingConcerns.Logging

Logging data models. A pure model layer, independent of logger implementations.

## Models

### LogDetail

The log record created for each request by the MediatR `LoggingBehavior`:

```csharp
public class LogDetail
{
    public string FullName { get; set; }       // Handler class name
    public string MethodName { get; set; }     // Method name
    public string User { get; set; }           // User name or "?"
    public string? TenantId { get; set; }      // Tenant Guid (multi-tenant)
    public List<LogParameter> Parameters { get; set; }  // Request parameters
}
```

### LogDetailWithException

For requests that raised an exception:

```csharp
public class LogDetailWithException : LogDetail
{
    public string ExceptionMessage { get; set; }
}
```

### LogParameter

```csharp
public class LogParameter
{
    public string Name { get; set; }   // Parameter name
    public object Value { get; set; }  // Parameter value
    public string Type { get; set; }   // .NET type name
}
```

### FileLogConfiguration

Serilog file sink configuration:

```json
{
  "FileLogConfiguration": {
    "FolderPath": "Logs",
    "MinLogLevel": "Warning",
    "LogOutputTemplate": "[{Timestamp:dd.MM.yyyy HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
    "SpecificLogFolders": ["Logs/UserService", "Logs/OrderService"]
  }
}
```
