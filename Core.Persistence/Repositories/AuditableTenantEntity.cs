namespace NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

/// <summary>
/// Opt-in base class for entities that need both tenant isolation and user-level audit tracking.
/// Extends <see cref="TenantEntity{TId}"/> and implements <see cref="IEntityAudit"/>.
/// Use this when an entity belongs to a tenant AND you want CreatedById/UpdatedById/DeletedById
/// populated automatically. Entities that need only one of the two should use
/// <see cref="TenantEntity{TId}"/> or <see cref="AuditableEntity{TId}"/> directly.
/// </summary>
public abstract class AuditableTenantEntity<TId> : TenantEntity<TId>, IEntityAudit
{
    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
    public Guid? DeletedById { get; set; }

    protected AuditableTenantEntity() { }

    protected AuditableTenantEntity(TId id) : base(id) { }
}
