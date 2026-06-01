using MediatR;
using Microsoft.AspNetCore.Http;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using NetCoreBackend.NArchitecture.Core.Security.Extensions;

namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Authorization;

// Blocks PlatformAdmin (is_super_admin=true) callers from invoking requests marked
// IBlockedForSuperAdmin. Impersonation (is_impersonating=true) is treated as a tenant-user
// context and allowed through.
public class SuperAdminBlockBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IBlockedForSuperAdmin
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SuperAdminBlockBehavior(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is not null
            && user.IsSuperAdmin()
            && !user.IsImpersonating())
        {
            throw new BusinessException(
                "This operation is for tenant users only. " +
                "Impersonate the target tenant before retrying.");
        }

        return await next();
    }
}
