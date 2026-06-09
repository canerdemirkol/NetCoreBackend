using MediatR;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;

namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Tenancy;

public class TenantValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ITenantValidationRequest
{
    private readonly ITenantContext _tenantContext;

    public TenantValidationBehavior(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsSuperAdmin && !_tenantContext.HasTenant)
            throw new AuthorizationException("Tenant context is required for this operation.");

        return await next(cancellationToken);
    }
}
