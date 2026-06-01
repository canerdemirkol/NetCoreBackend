namespace NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

public abstract class TenantEntity<TId> : Entity<TId>, ITenantEntity
{
    public Guid TenantId { get; set; }

    protected TenantEntity() { }

    protected TenantEntity(TId id) : base(id) { }
}
