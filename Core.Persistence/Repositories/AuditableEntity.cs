namespace NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

/// <summary>
/// Opt-in base class for entities that need user-level audit tracking.
/// Extend this instead of <see cref="Entity{TId}"/> when you want
/// <see cref="CreatedById"/>, <see cref="UpdatedById"/>, and <see cref="DeletedById"/>
/// populated automatically by <see cref="EfRepositoryBase{TEntity,TEntityId,TContext}"/>.
/// Entities that do not need audit should extend <see cref="Entity{TId}"/> directly.
/// </summary>
public abstract class AuditableEntity<TId> : Entity<TId>, IEntityAudit
{
    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
    public Guid? DeletedById { get; set; }

    protected AuditableEntity() { }

    protected AuditableEntity(TId id) : base(id) { }
}
