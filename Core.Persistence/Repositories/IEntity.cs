namespace NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

/// <summary>
/// Contract every persisted entity satisfies: an identifier of type <typeparamref name="T"/>.
/// <see cref="Entity{TId}"/> is the standard implementation; multi-tenant entities extend
/// <see cref="TenantEntity{TId}"/> which still implements this interface.
/// </summary>
public interface IEntity<T>
{
    T Id { get; set; }
}
