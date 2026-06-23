using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using NetCoreBackend.NArchitecture.Core.Security.Constants;
using NetCoreBackend.NArchitecture.Core.Security.Extensions;

namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Authorization;

public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ISecuredRequest
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthorizationBehavior(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        // HttpContext may be null when the pipeline is invoked outside an HTTP request
        // (e.g. background jobs, integration tests). Treat as unauthenticated.
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null || !user.Claims.Any())
            throw new AuthorizationException("You are not authenticated.");

        // SuperAdmin bypasses all role checks
        if (user.IsSuperAdmin())
            return await next(cancellationToken);

        if (request.Roles.Any())
        {
            ICollection<string>? userRoleClaims = user.GetRoleClaims() ?? [];

            // Tenant-level "Admin" bypasses role-specific checks within the tenant — BUT not when
            // the request explicitly requires "SuperAdmin". SuperAdmin gating must remain reachable
            // only by PlatformAdmin tokens (handled by the IsSuperAdmin() bypass above).
            bool requiresSuperAdmin = request.Roles.Contains(GeneralOperationClaims.SuperAdmin);

            bool isNotMatchedAUserRoleClaimWithRequestRoles = userRoleClaims
                .FirstOrDefault(userRoleClaim =>
                    (!requiresSuperAdmin && userRoleClaim == GeneralOperationClaims.Admin)
                    || request.Roles.Contains(userRoleClaim)
                )
                == null;
            if (isNotMatchedAUserRoleClaimWithRequestRoles)
                throw new AuthorizationException("You are not authorized.");
        }

        TResponse response = await next(cancellationToken);
        return response;
    }
}
