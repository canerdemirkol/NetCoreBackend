namespace NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

/// <summary>
/// Marker for tenant-scoped entities. Carries the foreign key to the owning tenant.
/// <see cref="EfRepositoryBase{TEntity, TEntityId, TContext}"/> auto-populates this on
/// <c>AddAsync</c> via <see cref="ITenantEntitySetter"/>, and EF Core's global query filter
/// (configured in the consuming app's DbContext) restricts reads to the current tenant.
/// Cross-tenant Update/Delete attempts are blocked at the repository layer.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
