using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCoreBackend.NArchitecture.Core.Outbox.Abstractions;
using NetCoreBackend.NArchitecture.Core.Outbox.Entities;

namespace NetCoreBackend.NArchitecture.Core.Outbox.Worker;

/// <summary>
/// <c>BackgroundService</c> that drains the outbox in a loop:
/// open a DI scope per iteration → fetch up to <c>OutboxOptions.BatchSize</c> due rows →
/// hand each to <c>IOutboxPublisher</c> → mark processed on success / record failure with
/// exponential backoff (or poison after <c>MaxAttempts</c>) on exception →
/// sleep <c>IdlePollDelay</c> if the batch was empty, otherwise loop immediately to drain.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Failure isolation:</strong> a single row throw never bubbles out of the loop.
/// The worker survives transient publisher outages, records each row's error individually,
/// and keeps shipping the rest of the batch.
/// </para>
/// <para>
/// <strong>Cancellation:</strong> <c>OperationCanceledException</c> tied to the host
/// stopping token exits the loop gracefully; per-message cancellation re-throws so the
/// message is NOT penalised — it is re-fetched on the next run.
/// </para>
/// <para>
/// <strong>Horizontal scaling:</strong> by default this worker assumes a single replica.
/// Running multiple instances against the same outbox table without external coordination
/// (e.g. leader election, <c>FOR UPDATE SKIP LOCKED</c>) risks duplicate publishes —
/// see <c>Core.Outbox/README.md §7.1</c>.
/// </para>
/// </remarks>
public sealed class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherWorker> _logger;
    private readonly OutboxOptions _options;

    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxPublisherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisherWorker started (batchSize={Batch}, maxAttempts={Max}).",
            _options.BatchSize, _options.MaxAttempts);

        while (!stoppingToken.IsCancellationRequested)
        {
            int dispatched;
            try
            {
                dispatched = await ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Defensive catch: if a batch fails wholesale (e.g. DB outage during Fetch),
                // log and pause before retrying so the host doesn't spin at 100% CPU.
                _logger.LogError(ex, "OutboxPublisherWorker batch failed; backing off.");
                dispatched = 0;
            }

            if (dispatched == 0)
            {
                try { await Task.Delay(_options.IdlePollDelay, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        _logger.LogInformation("OutboxPublisherWorker stopping.");
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IOutboxStore store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        IOutboxPublisher publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();

        IReadOnlyList<OutboxMessage> due = await store.FetchDueAsync(_options.BatchSize, cancellationToken).ConfigureAwait(false);
        if (due.Count == 0) return 0;

        int processed = 0;
        foreach (OutboxMessage message in due)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken).ConfigureAwait(false);
                await store.MarkProcessedAsync(message, cancellationToken).ConfigureAwait(false);
                processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Don't penalise the message for host shutdown — it will be re-fetched next run.
                throw;
            }
            catch (Exception ex)
            {
                int nextAttempt = message.AttemptCount + 1;
                bool poisoned = nextAttempt >= _options.MaxAttempts;
                DateTime? next = poisoned ? null : DateTime.UtcNow.Add(ComputeBackoff(nextAttempt));

                _logger.LogWarning(ex,
                    "Outbox publish failed for {EventType} (attempt {Attempt}/{Max}{Poisoned}).",
                    message.EventType, nextAttempt, _options.MaxAttempts, poisoned ? " — poisoned" : string.Empty);

                await store.RecordFailureAsync(message, ex.Message, next, poisoned, cancellationToken).ConfigureAwait(false);
            }
        }

        return processed;
    }

    private TimeSpan ComputeBackoff(int attempt)
    {
        // attempt is 1-based, so attempt=1 → BaseRetryDelay × 1, attempt=2 → ×2, ×4, ×8...
        double seconds = _options.BaseRetryDelay.TotalSeconds * Math.Pow(2, attempt - 1);
        TimeSpan capped = TimeSpan.FromSeconds(Math.Min(seconds, _options.MaxRetryDelay.TotalSeconds));
        return capped;
    }
}
