using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Performance;

public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IIntervalRequest
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        string requestName = request.GetType().Name;

        // Local stopwatch per invocation: avoids shared-instance corruption if a
        // single Stopwatch was previously registered in DI (singleton/scoped would
        // interleave measurements across concurrent requests).
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            return await next();
        }
        finally
        {
            stopwatch.Stop();
            if (stopwatch.Elapsed.TotalSeconds > request.Interval)
            {
                string message = $"Performance -> {requestName} {stopwatch.Elapsed.TotalSeconds} s";
                Debug.WriteLine(message);
                _logger.LogInformation(message);
            }
        }
    }
}
