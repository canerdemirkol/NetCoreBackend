namespace NetCoreBackend.NArchitecture.Core.Outbox.Worker;

public sealed class OutboxOptions
{
    // How many rows to lift per polling iteration. Trade off: larger batches reduce DB
    // round trips but inflate the blast radius if the consumer's publisher is slow.
    public int BatchSize { get; set; } = 50;

    // Sleep between polls when the previous round was empty. When a round returned rows the
    // worker loops immediately to drain backlogs without throttling.
    public TimeSpan IdlePollDelay { get; set; } = TimeSpan.FromSeconds(2);

    // Maximum number of dispatch attempts before a row is marked poisoned and removed from
    // the active rotation. Operators must intervene to retry poisoned rows.
    public int MaxAttempts { get; set; } = 8;

    // Base backoff for exponential retry. With default 2s and max attempts 8 the schedule
    // is roughly: 2s, 4s, 8s, 16s, 32s, 1m, 2m, 4m before poisoning.
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    // Cap on the computed retry delay so exponential growth doesn't push retries into
    // tomorrow when AttemptCount is high.
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(10);

    // Fail fast at startup. Silently coercing nonsense (BatchSize=0 → infinite idle loop,
    // MaxRetryDelay < BaseRetryDelay → first-attempt cap) costs hours to debug; loud
    // exceptions surface the misconfiguration during deployment instead of production.
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
