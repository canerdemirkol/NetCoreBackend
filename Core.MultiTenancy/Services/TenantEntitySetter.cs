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

    // Sets TenantId on the entity from the current tenant context.
    //
    // - Normal tenant user: pulls TenantId from context (always non-empty if middleware ran).
    // - SuperAdmin without impersonation: caller MUST set a non-empty TenantId on the entity itself;
    //   otherwise we'd silently write rows with TenantId = Guid.Empty that are invisible to every tenant.
    // - No context and not SuperAdmin: hard error — middleware did not run or tenant resolution failed.
    public void SetTenantId(ITenantEntity entity)
    {
        if (_tenantContext.TenantId.HasValue)
        {
            entity.TenantId = _tenantContext.TenantId.Value;
            return;
        }

        if (_tenantContext.IsSuperAdmin)
        {
            if (entity.TenantId == Guid.Empty)
                throw new InvalidOperationException(
                    $"Cannot add tenant entity '{entity.GetType().Name}' as SuperAdmin without a target tenant: " +
                    "explicitly set entity.TenantId before calling Add, or impersonate the target tenant.");
            return;
        }

        throw new InvalidOperationException(
            $"Cannot add tenant entity '{entity.GetType().Name}' without an active tenant context. " +
            "Ensure TenantMiddleware has resolved a tenant for the current request.");
    }
}
