using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Configurations;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Serilog;
using Serilog;
using Serilog.Events;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Serilog.File;

public class SerilogFileLogger : SerilogLoggerServiceBase
{
    public SerilogFileLogger(FileLogConfiguration configuration)
        : base(logger: null!)
    {
        // Use AppContext.BaseDirectory instead of Directory.GetCurrentDirectory() for IIS compatibility
        string baseDirectory = AppContext.BaseDirectory;

        // Normalize FolderPath with fallback - remove leading/trailing slashes and backslashes.
        // Also reject path traversal sequences and absolute paths — user-supplied config
        // could otherwise write logs outside the application directory.
        string normalizedFolderPath = !string.IsNullOrWhiteSpace(configuration?.FolderPath)
            ? configuration.FolderPath.Trim('/', '\\')
            : "logs";

        if (normalizedFolderPath.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(normalizedFolderPath))
            throw new ArgumentException(
                $"FileLogConfiguration.FolderPath '{normalizedFolderPath}' is not allowed: " +
                "path traversal ('..') and absolute paths are rejected.",
                nameof(configuration));

        // Parse minimum log level with fallback
        LogEventLevel minLogLevel = LogEventLevel.Debug;
        if (!string.IsNullOrWhiteSpace(configuration?.MinLogLevel))
        {
            if (!Enum.TryParse(configuration.MinLogLevel, ignoreCase: true, out minLogLevel))
            {
                // Surface the misconfiguration to first-chance debug listeners instead of swallowing silently
                System.Diagnostics.Debug.WriteLine(
                    $"[SerilogFileLogger] Unknown MinLogLevel '{configuration.MinLogLevel}', falling back to Debug.");
                minLogLevel = LogEventLevel.Debug;
            }
        }

        // Output template with fallback
        string outputTemplate = !string.IsNullOrWhiteSpace(configuration?.LogOutputTemplate)
            ? configuration.LogOutputTemplate
            : "[{Timestamp:dd.MM.yyyy HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(minLogLevel);

        // 1. General logs - SpecificLogFolders'a ait loglar hariç tüm loglar
        string generalLogPath = Path.Combine(
            baseDirectory,
            normalizedFolderPath,
            "GeneralLogs",
            "AllLogs.txt"
        );

        loggerConfig.WriteTo.Logger(lc => lc
            .Filter.ByExcluding(e =>
            {
                // SpecificLogFolders'da tanımlı folder isimlerini içeren mesajları hariç tut
                var message = e.RenderMessage();
                return configuration?.SpecificLogFolders?.Any(folder =>
                    !string.IsNullOrWhiteSpace(folder) && message.Contains($"[{folder}]")) ?? false;
            })
            .WriteTo.File(
                path: generalLogPath,
                restrictedToMinimumLevel: minLogLevel,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 50 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: outputTemplate
            )
        );

        // 2. Specific log folders - Configuration'dan gelen her servis için ayrı log klasörü
        if (configuration?.SpecificLogFolders != null && configuration.SpecificLogFolders.Any())
        {
            foreach (var logFolderName in configuration.SpecificLogFolders)
            {
                if (string.IsNullOrWhiteSpace(logFolderName))
                    continue; // Skip empty folder names

                string specificLogPath = Path.Combine(
                    baseDirectory,
                    normalizedFolderPath,
                    logFolderName,
                    $"{logFolderName}.txt"
                );

                loggerConfig.WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.RenderMessage().Contains($"[{logFolderName}]"))
                    .WriteTo.File(
                        path: specificLogPath,
                        restrictedToMinimumLevel: minLogLevel,
                        rollingInterval: RollingInterval.Day,
                        rollOnFileSizeLimit: true,
                        fileSizeLimitBytes: 50 * 1024 * 1024,
                        outputTemplate: outputTemplate
                    )
                );
            }
        }

        // 3. HTTP Request logs - HttpRequest property içeren loglar
        string httpLogPath = Path.Combine(
            baseDirectory,
            normalizedFolderPath,
            "HttpLogs",
            "HttpLog.txt"
        );

        loggerConfig.WriteTo.Conditional(
            logEvent => logEvent.Properties.ContainsKey("HttpRequest"),
            wt => wt.File(
                path: httpLogPath,
                restrictedToMinimumLevel: LogEventLevel.Information,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 50 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: outputTemplate
            )
        );

        Logger = loggerConfig.CreateLogger();
    }
}
