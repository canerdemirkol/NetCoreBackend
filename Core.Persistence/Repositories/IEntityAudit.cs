namespace NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

/// <summary>
/// User-level audit fields maintained by <see cref="EfRepositoryBase{TEntity, TEntityId, TContext}"/>
/// when an <see cref="ICurrentUserService"/> is registered:
/// <see cref="CreatedById"/> set on Add, <see cref="UpdatedById"/> set on Update,
/// <see cref="DeletedById"/> set on soft-delete. All nullable — system operations
/// (background jobs, migrations) produce no authenticated user.
/// </summary>
public interface IEntityAudit
{
    Guid? CreatedById { get; set; }
    Guid? UpdatedById { get; set; }
    Guid? DeletedById { get; set; }
}
