using Microsoft.AspNetCore.Builder;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi.Middleware;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi.Extensions;

public static class ApplicationBuilderExceptionMiddlewareExtensions
{
    public static void ConfigureCustomExceptionMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionMiddleware>();
    }
}
