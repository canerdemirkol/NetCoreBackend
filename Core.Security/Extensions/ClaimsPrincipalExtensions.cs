using System.Collections.Immutable;
using System.Security.Claims;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Constants;
using NetCoreBackend.NArchitecture.Core.Security.Constants;

namespace NetCoreBackend.NArchitecture.Core.Security.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static ICollection<string>? GetClaims(this ClaimsPrincipal claimsPrincipal, string claimType)
    {
        return claimsPrincipal?.FindAll(claimType)?.Select(x => x.Value).ToImmutableArray();
    }

    public static ICollection<string>? GetRoleClaims(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal?.GetClaims(ClaimTypes.Role);
    }

    public static string? GetIdClaim(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public static Guid? GetTenantId(this ClaimsPrincipal claimsPrincipal)
    {
        string? value = claimsPrincipal?.FindFirst(TenantClaimTypes.TenantId)?.Value;
        return Guid.TryParse(value, out Guid tenantId) ? tenantId : null;
    }

    public static bool IsSuperAdmin(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal?.FindFirst(TenantClaimTypes.IsSuperAdmin)?.Value == "true";
    }

    public static bool IsImpersonating(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal?.FindFirst(TenantClaimTypes.IsImpersonating)?.Value == "true";
    }

    // User-level impersonation: the token's primary identity is the impersonated user and the
    // real actor travels in ImpersonationClaimTypes. Distinct from IsImpersonating(), which marks
    // a platform admin operating within a tenant scope with their own identity.
    public static bool IsUserImpersonation(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal?.FindFirst(ImpersonationClaimTypes.ImpersonatorId) is not null;
    }

    public static string? GetImpersonatorIdClaim(this ClaimsPrincipal claimsPrincipal)
    {
        return claimsPrincipal?.FindFirst(ImpersonationClaimTypes.ImpersonatorId)?.Value;
    }
}
