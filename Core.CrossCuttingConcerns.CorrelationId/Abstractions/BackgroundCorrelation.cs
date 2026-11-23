namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.Abstractions;

/// <summary>
/// A correlation id for background work (a scheduled job's run), where no HTTP request carries one. A run opens it; everything written in
/// that run (logs, audit and change records) carries the same id. It flows with the async context, so runs side by side never share one,
/// and a nested run restores the outer id when it ends. <see cref="ICorrelationIdAccessor"/> implementations fall back to it.
/// </summary>
public static class BackgroundCorrelation
{
    private static readonly AsyncLocal<string?> Ambient = new();

    public static string? Current => Ambient.Value;

    /// <summary>Starts a run: "job-{job}-{guid}". Dispose to end it.</summary>
    public static IDisposable Begin(string job)
    {
        string? outer = Ambient.Value;
        Ambient.Value = $"job-{job}-{Guid.NewGuid():N}";
        return new Run(outer);
    }

    private sealed class Run(string? outer) : IDisposable
    {
        public void Dispose() => Ambient.Value = outer;
    }
}
