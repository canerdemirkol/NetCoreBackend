using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;

namespace NetCoreBackend.NArchitecture.Core.MultiTenancy.Context;

public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public string? TenantIdentifier { get; private set; }
    public bool IsSuperAdmin { get; private set; }
    public bool IsImpersonating { get; private set; }
    public bool HasTenant => TenantId.HasValue;
    public string? DefaultLocale { get; private set; }

    public void SetTenant(Guid tenantId, string identifier, string? defaultLocale = null)
    {
        TenantId = tenantId;
        TenantIdentifier = identifier;
        DefaultLocale = defaultLocale;
    }

    public void SetSuperAdmin()
    {
        IsSuperAdmin = true;
    }

    public void SetImpersonating()
    {
        IsImpersonating = true;
    }
}
