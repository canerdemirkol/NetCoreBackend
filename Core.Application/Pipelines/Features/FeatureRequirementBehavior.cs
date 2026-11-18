using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;

namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Features;

/// <summary>
/// Stops an <see cref="IFeatureRequest"/> with 403 when the caller's tenant lacks one of its features: the caller is known, the
/// package does not include the operation. The host (super admin, not impersonating) has no package and is not checked; the host
/// impersonating a tenant gets that tenant's features.
/// </summary>
public class FeatureRequirementBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IFeatureRequest
{
    private readonly IFeatureChecker _featureChecker;
    private readonly ITenantContext _tenantContext;

    public FeatureRequirementBehavior(IFeatureChecker featureChecker, ITenantContext tenantContext)
    {
        _featureChecker = featureChecker;
        _tenantContext = tenantContext;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_tenantContext.IsSuperAdmin && !_tenantContext.IsImpersonating)
            return await next(cancellationToken);

        if (_tenantContext.TenantId is not Guid tenantId)
            throw new ForbiddenException("Features cannot be checked without a tenant.");

        foreach (string feature in request.RequiredFeatures)
        {
            if (!await _featureChecker.IsEnabledAsync(tenantId, feature, cancellationToken))
                throw new ForbiddenException($"Feature '{feature}' is not available in the current edition.");
        }

        return await next(cancellationToken);
    }
}
