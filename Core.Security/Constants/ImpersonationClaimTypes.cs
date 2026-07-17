namespace NetCoreBackend.NArchitecture.Core.Security.Constants;

/// <summary>
/// Claim types for user-level impersonation ("login as this user"). The token's primary
/// identity is the impersonated user; these claims carry the real actor for audit and
/// back-to-impersonator flows. Absence of ImpersonatorId means the token is not a
/// user-impersonation token. Distinct from tenant impersonation (TenantClaimTypes.IsImpersonating),
/// where the identity stays the platform admin and only the data scope changes.
/// </summary>
public static class ImpersonationClaimTypes
{
    public const string ImpersonatorId = "impersonator_id";
    public const string ImpersonatorType = "impersonator_type";
    public const string ImpersonatorTenantId = "impersonator_tenant_id";

    public static class ImpersonatorTypes
    {
        public const string PlatformAdmin = "platform_admin";
        public const string TenantUser = "tenant_user";
    }
}
