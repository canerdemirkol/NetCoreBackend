using Microsoft.AspNetCore.Builder;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.WebApi.Middleware;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.WebApi.Extensions;

public static class ApplicationBuilderCorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }
}
