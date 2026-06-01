# Core.CrossCuttingConcerns.Logging.DependencyInjection

Logger implementasyonunu DI container'a kaydeden extension metot.

## Kurulum

```csharp
// Program.cs — SerilogFileLogger ile
builder.Services.AddSingleton<ILogger, SerilogFileLogger>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new SerilogFileLogger(
        config.GetSection("FileLogConfiguration").Get<FileLogConfiguration>()!);
});
```

Ya da `ServiceCollectionLoggingExtensions` extension metodu aracılığıyla:

```csharp
builder.Services.AddLoggingServices(config.GetSection("FileLogConfiguration")
    .Get<FileLogConfiguration>()!);
```

`ILogger` singleton olarak kaydedilir — tüm request'ler aynı logger instance'ını paylaşır.
