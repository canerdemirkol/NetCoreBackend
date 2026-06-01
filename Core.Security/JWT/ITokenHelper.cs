using NetCoreBackend.NArchitecture.Core.Security.Entities;

namespace NetCoreBackend.NArchitecture.Core.Security.JWT;

public interface ITokenHelper<TUserId, TOperationClaimId, TRefreshTokenId>
{
    // Tenant user — issues a token scoped to the user's tenant
    AccessToken CreateToken(User<TUserId> user, IList<OperationClaim<TOperationClaimId>> operationClaims);
    RefreshToken<TRefreshTokenId, TUserId> CreateRefreshToken(User<TUserId> user, string ipAddress);

    // PlatformAdmin — issues a platform-wide SuperAdmin token (no tenant)
    AccessToken CreateAdminToken(PlatformAdmin<TUserId> admin, IList<OperationClaim<TOperationClaimId>> operationClaims);

    // PlatformAdmin impersonation — issues a SuperAdmin token scoped to a specific tenant
    AccessToken CreateImpersonationToken(PlatformAdmin<TUserId> admin, IList<OperationClaim<TOperationClaimId>> operationClaims, Guid tenantId);
}
