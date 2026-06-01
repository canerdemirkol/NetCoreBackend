using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Test.Application.FakeData;

public abstract class BaseFakeData<TEntity, TEntityId>
    where TEntity : Entity<TEntityId>, new()
{
    private List<TEntity>? _cachedData;

    // Lazy-cached so repeated access returns the SAME list with the same Ids.
    // Random generators (Faker, RandomNumberGenerator) would otherwise yield
    // different values on every property read, breaking test invariants.
    public List<TEntity> Data => _cachedData ??= CreateFakeData();

    public abstract List<TEntity> CreateFakeData();
}
