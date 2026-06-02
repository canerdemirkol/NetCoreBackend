namespace NetCoreBackend.NArchitecture.Core.Outbox.Worker;

/// <summary>
/// Runtime knobs for <c>OutboxPublisherWorker</c>. Bind from <c>appsettings.json</c>
/// (<c>"OutboxOptions": { … }</c>) via <c>services.Configure&lt;OutboxOptions&gt;(...)</c>
/// or pass an inline <see cref="System.Action{T}"/> to
/// <c>services.AddOutbox&lt;TDbContext&gt;(opt =&gt; { ... })</c>.
/// </summary>
/// <remarks>
/// <see cref="Validate"/> is invoked via <c>ValidateOnStart()</c> during host build,
/// so a misconfiguration (e.g. <c>BatchSize=0</c>, <c>MaxRetryDelay &lt; BaseRetryDelay</c>)
/// fails fast at deployment instead of silently degrading at runtime.
/// </remarks>
public sealed class OutboxOptions
{
    /// <summary>
    /// How many rows the worker lifts per polling iteration. Trade-off: larger batches
    /// reduce DB round trips but inflate the blast radius if the publisher is slow.
    /// Default: <c>50</c>.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Sleep between polls when the previous round returned no rows. When a round returned
    /// rows, the worker loops immediately to drain the backlog without throttling.
    /// Default: <c>2 seconds</c>.
    /// </summary>
    public TimeSpan IdlePollDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum number of dispatch attempts before a row is marked poisoned and removed
    /// from the active rotation. Operators must intervene to retry poisoned rows.
    /// Default: <c>8</c>.
    /// </summary>
    public int MaxAttempts { get; set; } = 8;

    /// <summary>
    /// Base backoff for exponential retry: <c>BaseRetryDelay × 2^(attempt - 1)</c>.
    /// With defaults (2 s base, 8 max attempts), the schedule is roughly
    /// <c>2 s → 4 s → 8 s → 16 s → 32 s → 1 m → 2 m → 4 m → POISONED</c>.
    /// Default: <c>2 seconds</c>.
    /// </summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Upper bound on the computed retry delay so exponential growth doesn't push retries
    /// into next week when <c>AttemptCount</c> is high. Default: <c>10 minutes</c>.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Fail fast at startup when configuration values are nonsense. Called by
    /// <c>OutboxServiceRegistration.AddOutbox</c> via <c>ValidateOnStart()</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Any field is out of its acceptable
    /// range (non-positive sizes/delays, or <c>MaxRetryDelay &lt; BaseRetryDelay</c>).</exception>
    public void Validate()
    {
        if (BatchSize <= 0)
            throw new InvalidOperationException($"OutboxOptions.BatchSize must be > 0 (was {BatchSize}).");
        if (MaxAttempts <= 0)
            throw new InvalidOperationException($"OutboxOptions.MaxAttempts must be > 0 (was {MaxAttempts}).");
        if (IdlePollDelay <= TimeSpan.Zero)
            throw new InvalidOperationException($"OutboxOptions.IdlePollDelay must be positive (was {IdlePollDelay}).");
        if (BaseRetryDelay <= TimeSpan.Zero)
            throw new InvalidOperationException($"OutboxOptions.BaseRetryDelay must be positive (was {BaseRetryDelay}).");
        if (MaxRetryDelay < BaseRetryDelay)
            throw new InvalidOperationException(
                $"OutboxOptions.MaxRetryDelay ({MaxRetryDelay}) must be >= BaseRetryDelay ({BaseRetryDelay}).");
    }
}
