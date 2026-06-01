namespace NetCoreBackend.NArchitecture.Core.Security.Constants;

public static class GeneralOperationClaims
{
    // Tenant-level administrator. Bypasses role-specific checks WITHIN their tenant
    // (see AuthorizationBehavior). Does NOT grant SuperAdmin privileges.
    public const string Admin = "Admin";

    // Platform-level administrator role name carried by PlatformAdmin tokens.
    // When a request's Roles include this value, the tenant Admin bypass does not apply —
    // only callers whose JWT is_super_admin=true may reach the handler.
    public const string SuperAdmin = "SuperAdmin";
}
