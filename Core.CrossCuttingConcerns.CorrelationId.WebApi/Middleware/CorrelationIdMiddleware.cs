using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.WebApi.Middleware;

/// <summary>
/// Reads or generates a correlation ID per request, propagates it as the X-Correlation-Id
/// response header, and enriches Serilog LogContext for the duration of the request.
/// </summary>
public class CorrelationIdMiddleware
{
    internal const string HttpItemsKey = "CorrelationId";
    private const string HeaderName = "X-Correlation-Id";

    // Caps client-supplied IDs to prevent oversized values from reaching log sinks or response headers.
    private const int MaxCorrelationIdLength = 128;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ResolveCorrelationId(context.Request);

        context.Items[HttpItemsKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
                context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpRequest request)
    {
        // Priority 1: client-supplied ID (API gateway / service mesh propagation)
        if (request.Headers.TryGetValue(HeaderName, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue))
        {
            string id = headerValue.ToString();
            return id.Length <= MaxCorrelationIdLength ? id : id[..MaxCorrelationIdLength];
        }

        // Priority 2: active distributed trace — aligns CorrelationId with OpenTelemetry/AppInsights spans
        string? traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrEmpty(traceId))
            return traceId;

        // Priority 3: standalone fallback
        return Guid.NewGuid().ToString("D");
    }
}
