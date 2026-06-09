using Microsoft.AspNetCore.Http;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.Abstractions;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.WebApi.Middleware;

namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.WebApi.Accessors;

/// <summary>Reads the correlation ID stored in HttpContext.Items by <see cref="CorrelationIdMiddleware"/>.</summary>
public class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? CorrelationId =>
        _httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.HttpItemsKey] as string;
}
