namespace NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

public interface ITenantEntitySetter
{
    Guid? CurrentTenantId { get; }
    bool IsSuperAdmin { get; }
    void SetTenantId(ITenantEntity entity);
}
