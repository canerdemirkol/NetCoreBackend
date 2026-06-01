# Core.CrossCuttingConcerns.Logging

Logging veri modelleri. Logger implementasyonlarından bağımsız, saf model katmanı.

## Modeller

### LogDetail

MediatR `LoggingBehavior` tarafından her request için oluşturulan log kaydı:

```csharp
public class LogDetail
{
    public string FullName { get; set; }       // Handler sınıf adı
    public string MethodName { get; set; }     // Method adı
    public string User { get; set; }           // Kullanıcı adı veya "?"
    public string? TenantId { get; set; }      // Tenant Guid (multi-tenant)
    public List<LogParameter> Parameters { get; set; }  // Request parametreleri
}
```

### LogDetailWithException

Exception olan request'ler için:

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
    public string Name { get; set; }    // Parametre adı
    public object? Value { get; set; } // Parametre değeri
    public string Type { get; set; }   // .NET tip adı
}
```

### FileLogConfiguration

Serilog file sink konfigürasyonu:

```json
{
  "FileLogConfiguration": {
    "FolderPath": "Logs",
    "MinimumLogEventLevel": "Warning",
    "OutputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] {Message}{NewLine}{Exception}",
    "SpecificFolderPaths": {
      "UserService": "Logs/UserService"
    }
  }
}
```
