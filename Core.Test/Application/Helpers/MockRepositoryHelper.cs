using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using NetCoreBackend.NArchitecture.Core.Persistence.Paging;
using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Test.Application.Helpers;

public static class MockRepositoryHelper
{
    public static Mock<TRepository> GetRepository<TRepository, TEntity, TEntityId>(List<TEntity> list)
        where TEntity : Entity<TEntityId>, new()
        where TRepository : class, IAsyncRepository<TEntity, TEntityId>, IRepository<TEntity, TEntityId>
    {
        var mockRepo = new Mock<TRepository>();

        Build<TRepository, TEntity, TEntityId>(mockRepo, list);
        return mockRepo;
    }

    private static void Build<TRepository, TEntity, TEntityId>(Mock<TRepository> mockRepo, List<TEntity> entityList)
        where TEntity : Entity<TEntityId>, new()
        where TRepository : class, IAsyncRepository<TEntity, TEntityId>, IRepository<TEntity, TEntityId>
    {
        SetupGetListAsync<TRepository, TEntity, TEntityId>(mockRepo, entityList);
        SetupGetAsync<TRepository, TEntity, TEntityId>(mockRepo, entityList);
        SetupAddAsync<TRepository, TEntity, TEntityId>(mockRepo, entityList);
        SetupUpdateAsync<TRepository, TEntity, TEntityId>(mockRepo, entityList);
        SetupDeleteAsync<TRepository, TEntity, TEntityId>(mockRepo, entityList);
        SetupAnyAsync<TRepository, TEntity, TEntityId>(mockRepo, entityList);
    }

    private static void SetupGetListAsync<TRepository, TEntity, TEntityId>(Mock<TRepository> mockRepo, List<TEntity> entityList)
        where TEntity : Entity<TEntityId>, new()
        where TRepository : class, IAsyncRepository<TEntity, TEntityId>, IRepository<TEntity, TEntityId>
    {
        mockRepo
            .Setup(s =>
                s.GetListAsync(
                    It.IsAny<Expression<Func<TEntity, bool>>>(),
                    It.IsAny<Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>>(),
                    It.IsAny<Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (
                    Expression<Func<TEntity, bool>> expression,
                    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy,
                    Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> include,
                    int index,
                    int size,
                    bool withDeleted,
                    bool enableTracking,
                    CancellationToken cancellationToken
                ) =>
                {
                    IEnumerable<TEntity> query = entityList;
                    if (!withDeleted)
                        query = query.Where(e => !e.DeletedDate.HasValue);
                    if (expression != null)
                        query = query.Where(expression.Compile());

                    // Use the ctor that computes paging metadata (Count/Pages/HasNext) — the
                    // parameterless `new()` left them at zero, so tests asserting on paging
                    // state would see HasNext=false regardless of how much data exists.
                    List<TEntity> filtered = query.ToList();
                    Paginate<TEntity> paginateList = size > 0
                        ? new Paginate<TEntity>(filtered, index, size, from: 0)
                        : new Paginate<TEntity> { Items = filtered };
                    return paginateList;
                }
            );
    }

    private static void SetupGetAsync<TRepository, TEntity, TEntityId>(Mock<TRepository> mockRepo, List<TEntity> entityList)
        where TEntity : Entity<TEntityId>, new()
        where TRepository : class, IAsyncRepository<TEntity, TEntityId>, IRepository<TEntity, TEntityId>
    {
        mockRepo
            .Setup(s =>
                s.GetAsync(
                    It.IsAny<Expression<Func<TEntity, bool>>>(),
                    It.IsAny<Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (
                    Expression<Func<TEntity, bool>> expression,
                    Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>> include,
                    bool withDeleted,
                    bool enableTracking,
                    CancellationToken cancellationToken
                ) =>
                {
                    IEnumerable<TEntity> query = entityList;
                    if (!withDeleted)
                        query = query.Where(e => !e.DeletedDate.HasValue);
                    return query.FirstOrDefault(predicate: expression.Compile());
                }
            );
    }

    private static void SetupAddAsync<TRepository, TEntity, TEntityId>(Mock<TRepository> mockRepo, List<TEntity> entityList)
        where TEntity : Entity<TEntityId>, new()
        where TRepository : class, IAsyncRepository<TEntity, TEntityId>, IRepository<TEntity, TEntityId>
    {
        mockRepo
            .Setup(r => r.AddAsync(It.IsAny<TEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (TEntity entity, CancellationToken cancellationToken) =>
                {
                    entityList.Add(entity);
                    return entity;
                }
            );
    }

    private static void SetupUpdateAsync<TRepository, TEntity, TEntityId2>(Mock<TRepository> mockRepo, List<TEntity> entityList)
        where TEntity : Entity<TEntityId2>, new()
        where TRepository : class, IAsyncRepository<TEntity, TEntityId2>, IRepository<TEntity, TEntityId2>
    {
        mockRepo
            .Setup(r => r.UpdateAsync(It.IsAny<TEntity>(), It.IsAny<CancellationToken>()))!
            .ReturnsAsync(
                (TEntity entity, CancellationToken cancellationToken) =>
                {
                    int index = entityList.FindIndex(x => x.Id!.Equals(entity.Id));
                    if (index >= 0)
                        entityList[index] = entity;
                    return entity;
                }
            );
    }

    private static void SetupDeleteAsync<TRepository, TEntity, TEntityId>(Mock<TRepository> mockRepo, List<TEntity> entityList)
        where TEntity : Entity<TEntityId>, new()
        where TRepository : class, IAsyncRepository<TEntity, TEntityId>, IRepository<TEntity, TEntityId>
    {
        mockRepo
            .Setup(r => r.DeleteAsync(It.IsAny<TEntity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (TEntity entity, bool permanent, CancellationToken cancellationToken) =>
                {
                    if (!permanent)
                        entity.DeletedDate = DateTime.UtcNow;
                    else
                        entityList.Remove(entity);
                    return entity;
                }
            );
    }

    public static void SetupAnyAsync<TRepository, TEntity, TEntityId>(Mock<TRepository> mockRepo, List<TEntity> entityList)
        where TEntity : Entity<TEntityId>, new()
        where TRepository : class, IAsyncRepository<TEntity, TEntityId>, IRepository<TEntity, TEntityId>
    {
        mockRepo
            .Setup(s =>
                s.AnyAsync(
                    It.IsAny<Expression<Func<TEntity, bool>>>(),
                    It.IsAny<Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>?>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (
                    Expression<Func<TEntity, bool>> expression,
                    Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include,
                    bool withDeleted,
                    CancellationToken cancellationToken
                ) =>
                {
                    IEnumerable<TEntity> query = entityList;
                    if (!withDeleted)
                        query = query.Where(e => !e.DeletedDate.HasValue);
                    return expression == null ? query.Any() : query.Any(expression.Compile());
                }
            );
    }
}
