using Microsoft.AspNetCore.Http;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.WebApi.Middleware;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.WebApi.Extensions;

public static class HttpContextCorrelationIdExtensions
{
    /// <summary>Returns the correlation ID assigned by <see cref="CorrelationIdMiddleware"/>, or null if the middleware has not run.</summary>
    public static string? GetCorrelationId(this HttpContext context)
        => context.Items[CorrelationIdMiddleware.HttpItemsKey] as string;
}
