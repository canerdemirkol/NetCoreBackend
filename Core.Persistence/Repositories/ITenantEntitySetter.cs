namespace NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

/// <summary>
/// Bridge between the multi-tenancy layer and the repository layer.
/// <see cref="EfRepositoryBase{TEntity, TEntityId, TContext}"/> depends on this — never on the
/// HTTP-tier <c>ITenantContext</c> directly — so the persistence library stays free of
/// HTTP/middleware references.
/// </summary>
public interface ITenantEntitySetter
{
    /// <summary>Current tenant from the request context, or <c>null</c> for SuperAdmin / no context.</summary>
    Guid? CurrentTenantId { get; }

    /// <summary>True when the caller is a PlatformAdmin. Repository guards allow cross-tenant
    /// reads/writes for SuperAdmin; ordinary tenant users are restricted to their own data.</summary>
    bool IsSuperAdmin { get; }

    /// <summary>Stamps <see cref="ITenantEntity.TenantId"/> with the current tenant on Add.
    /// Throws when no tenant context is available and the caller is not SuperAdmin —
    /// prevents accidental writes with <c>Guid.Empty</c>.</summary>
    void SetTenantId(ITenantEntity entity);
}
