using MediatR;
using Microsoft.AspNetCore.Http;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using NetCoreBackend.NArchitecture.Core.Security.Extensions;

namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Tenancy;

public class TenantValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ITenantValidationRequest
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantValidationBehavior(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        bool isSuperAdmin = _httpContextAccessor.HttpContext?.User.IsSuperAdmin() ?? false;

        if (!isSuperAdmin)
        {
            Guid? tenantId = _httpContextAccessor.HttpContext?.User.GetTenantId();
            if (!tenantId.HasValue)
                throw new AuthorizationException("Tenant context is required for this operation.");
        }

        return await next();
    }
}
