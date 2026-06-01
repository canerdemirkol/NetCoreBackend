namespace NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;

public interface ITenantContext
{
    Guid? TenantId { get; }
    string? TenantIdentifier { get; }
    bool IsSuperAdmin { get; }
    bool IsImpersonating { get; }
    bool HasTenant { get; }
    // BCP 47 fallback locale configured on the tenant (used when client sends no Accept-Language).
    string? DefaultLocale { get; }
}
