namespace NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

/// <summary>
/// Minimal contract exposing the underlying <see cref="IQueryable{T}"/> root.
/// Both <see cref="IRepository{TEntity, TEntityId}"/> and <see cref="IAsyncRepository{TEntity, TEntityId}"/>
/// extend this so callers can compose ad-hoc projections without the repository having to
/// surface every LINQ overload through CRUD methods.
/// </summary>
public interface IQuery<T>
{
    IQueryable<T> Query();
}
