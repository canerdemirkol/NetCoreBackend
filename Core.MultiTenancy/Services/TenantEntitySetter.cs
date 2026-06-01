using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;
using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.MultiTenancy.Services;

public class TenantEntitySetter : ITenantEntitySetter
{
    private readonly ITenantContext _tenantContext;

    public TenantEntitySetter(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public Guid? CurrentTenantId => _tenantContext.TenantId;
    public bool IsSuperAdmin => _tenantContext.IsSuperAdmin;

    public void SetTenantId(ITenantEntity entity)
    {
        if (_tenantContext.TenantId.HasValue)
            entity.TenantId = _tenantContext.TenantId.Value;
    }
}
