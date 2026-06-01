namespace NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

/// <summary>
/// Audit/soft-delete timestamps maintained by <see cref="EfRepositoryBase{TEntity, TEntityId, TContext}"/>:
/// <see cref="CreatedDate"/> set on Add, <see cref="UpdatedDate"/> set on Update,
/// <see cref="DeletedDate"/> set on Delete (when <c>permanent=false</c>). A non-null
/// <see cref="DeletedDate"/> marks the row as soft-deleted; default queries hide such rows
/// (use <c>withDeleted: true</c> to include them).
/// </summary>
public interface IEntityTimestamps
{
    DateTime CreatedDate { get; set; }
    DateTime? UpdatedDate { get; set; }
    DateTime? DeletedDate { get; set; }
}
