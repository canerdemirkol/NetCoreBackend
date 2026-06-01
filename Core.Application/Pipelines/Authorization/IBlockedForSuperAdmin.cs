namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Authorization;

// Marker for handlers that must run only in a tenant-user context, never as PlatformAdmin.
//
// AuthorizationBehavior gives SuperAdmin tokens unconditional access — appropriate for most
// management endpoints, but wrong for operations that are semantically a tenant-user action
// (updating one's own profile, posting to a per-tenant activity feed, accepting a friend
// request, etc.). Mark such requests with this interface; SuperAdminBlockBehavior will reject
// them when the caller is a PlatformAdmin who has not impersonated a tenant.
public interface IBlockedForSuperAdmin { }
