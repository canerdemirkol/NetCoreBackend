using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using EfQuery = Microsoft.EntityFrameworkCore.Query;
using NetCoreBackend.NArchitecture.Core.Persistence.Dynamic;
using NetCoreBackend.NArchitecture.Core.Persistence.Paging;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;

namespace NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

public class EfRepositoryBase<TEntity, TEntityId, TContext>
    : IAsyncRepository<TEntity, TEntityId>,
        IRepository<TEntity, TEntityId>
    where TEntity : Entity<TEntityId>
    where TContext : DbContext
{
    protected readonly TContext Context;
    protected readonly ITenantEntitySetter? TenantSetter;
    protected readonly ICurrentUserService? CurrentUserService;

    // Convenience accessor for raw SQL methods that bypass EF Core global query filters.
    // Always pass this as a parameter when constructing WHERE TenantId = @tenantId clauses.
    protected Guid? CurrentTenantId => TenantSetter?.CurrentTenantId;
    protected Guid? CurrentUserId => CurrentUserService?.UserId;

    public EfRepositoryBase(TContext context, ITenantEntitySetter? tenantSetter = null, ICurrentUserService? currentUserService = null)
    {
        Context = context;
        TenantSetter = tenantSetter;
        CurrentUserService = currentUserService;
    }

    public IQueryable<TEntity> Query()
    {
        return Context.Set<TEntity>();
    }

    // Safe: FromSqlRaw goes through DbSet → EF Core wraps it as subquery → global query filter (TenantId) is applied automatically.
    public IList<TResult> ExecuteSqlCommand<TResult>(string sql, object[]? parameters = null) where TResult : Entity<TEntityId>, new()
    {
        return parameters is null
            ? Context.Set<TResult>().FromSqlRaw(sql).ToList()
            : Context.Set<TResult>().FromSqlRaw(sql, parameters).ToList();
    }

    // WARNING: Database.ExecuteSqlRawAsync bypasses EF Core global query filters entirely.
    // If TEntity is a tenant-aware entity, callers MUST include WHERE TenantId = @tenantId in the SQL
    // and pass CurrentTenantId as a parameter. SuperAdmin is exempt.
    public async Task<int> ExecuteSqlRawAsync(string sql, object[]? parameters = null)
    {
        GuardTenantContext();
        return parameters == null
            ? await Context.Database.ExecuteSqlRawAsync(sql).ConfigureAwait(false)
            : await Context.Database.ExecuteSqlRawAsync(sql, parameters).ConfigureAwait(false);
    }

    // WARNING: Stored procedures bypass EF Core global query filters.
    // The procedure itself must enforce tenant isolation via TenantId parameter.
    // SuperAdmin is exempt.
    public async Task<int> ExecuteStoredProcedureAsync(string procedure, object[]? parameters = null)
    {
        GuardTenantContext();
        string command = $"BEGIN {procedure}; END;";
        return parameters == null
            ? await Context.Database.ExecuteSqlRawAsync(command).ConfigureAwait(false)
            : await Context.Database.ExecuteSqlRawAsync(command, parameters).ConfigureAwait(false);
    }

    // Tenant-safe bulk update (EF Core 7+ ExecuteUpdate).
    //
    // EF Core's ExecuteUpdate/ExecuteDelete emit a single SQL UPDATE/DELETE statement and DO
    // apply the global query filter — but a caller can defeat that by writing `Query()` and
    // chaining `IgnoreQueryFilters()` themselves. These wrappers start from Query() (which
    // carries the filter) and additionally compose the predicate, then verify a tenant
    // context exists before issuing the statement. SuperAdmin is exempt from the context
    // check just like in raw SQL.
    public async Task<int> ExecuteUpdateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<EfQuery.UpdateSettersBuilder<TEntity>> setPropertyCalls,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(setPropertyCalls);
        GuardTenantContext();
        return await Query().Where(predicate).ExecuteUpdateAsync(setPropertyCalls, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> ExecuteDeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        GuardTenantContext();
        return await Query().Where(predicate).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    // Removes the soft-delete filter while keeping tenant isolation intact.
    // IgnoreQueryFilters() strips ALL global filters including the tenant filter, so after
    // calling it we manually re-apply the tenant predicate for non-SuperAdmin callers.
    private IQueryable<TEntity> ApplyIncludeDeleted(IQueryable<TEntity> queryable)
    {
        queryable = queryable.IgnoreQueryFilters();
        if (typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity))
            && TenantSetter is { IsSuperAdmin: false, CurrentTenantId: { } tenantId })
        {
            queryable = queryable.Where(e => ((ITenantEntity)e).TenantId == tenantId);
        }
        return queryable;
    }

    // Throws if the entity is tenant-aware but no tenant context is present (and caller is not SuperAdmin).
    // Prevents accidental cross-tenant data mutations via raw SQL.
    private void GuardTenantContext()
    {
        if (TenantSetter is null) return;                          // no multi-tenancy wired up
        if (TenantSetter.IsSuperAdmin) return;                    // SuperAdmin may cross tenants
        if (typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity)) && !CurrentTenantId.HasValue)
            throw new InvalidOperationException(
                $"Raw SQL on tenant entity '{typeof(TEntity).Name}' requires an active tenant context. " +
                "Ensure TenantMiddleware has run and the request carries a valid tenant_id claim.");
    }

    // Verifies entity belongs to the current tenant before Update/Delete operations.
    // EF Core's global query filter does NOT apply to Update/Delete by primary key —
    // without this guard, a tenant could mutate entities belonging to another tenant
    // by submitting a payload containing a foreign Id.
    private void GuardTenantOwnership(TEntity entity)
    {
        if (TenantSetter is null) return;
        if (TenantSetter.IsSuperAdmin) return;
        if (entity is not ITenantEntity tenantEntity) return;

        if (!CurrentTenantId.HasValue)
            throw new InvalidOperationException(
                $"Write on tenant entity '{typeof(TEntity).Name}' requires an active tenant context.");

        if (tenantEntity.TenantId != CurrentTenantId.Value)
            throw new InvalidOperationException(
                $"Cross-tenant write blocked: entity TenantId '{tenantEntity.TenantId}' " +
                $"does not match current tenant '{CurrentTenantId.Value}'.");
    }

    private void GuardTenantOwnership(IEnumerable<TEntity> entities)
    {
        foreach (TEntity entity in entities)
            GuardTenantOwnership(entity);
    }

    // Rejects update/delete attempts on entities whose Id is still the type default
    // (Guid.Empty for Guid, 0 for int, null for ref types). EF Core would otherwise issue
    // `WHERE Id = @default` and either fail or touch the wrong row.
    private static void GuardValidId(TEntity entity)
    {
        if (EqualityComparer<TEntityId>.Default.Equals(entity.Id, default!))
            throw new InvalidOperationException(
                $"Cannot operate on entity '{typeof(TEntity).Name}' with a default/empty Id. " +
                "Load the entity first or set a valid Id before calling Update/Delete.");
    }

    private static void GuardValidId(IEnumerable<TEntity> entities)
    {
        foreach (TEntity entity in entities)
            GuardValidId(entity);
    }

    protected virtual void EditEntityPropertiesToAdd(TEntity entity)
    {
        entity.CreatedDate = DateTime.UtcNow;
        if (entity is IEntityAudit auditOnAdd)
            auditOnAdd.CreatedById = CurrentUserId;
        if (entity is ITenantEntity tenantEntity)
        {
            // Fail-fast: without TenantSetter the row would be written with TenantId == default and
            // become invisible to every tenant. Loud error beats silent data corruption.
            if (TenantSetter is null)
                throw new InvalidOperationException(
                    $"Cannot add tenant entity '{typeof(TEntity).Name}': ITenantEntitySetter is not registered. " +
                    "Call services.AddMultiTenancy() in the consuming application's DI setup.");
            TenantSetter.SetTenantId(tenantEntity);
        }
    }

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        EditEntityPropertiesToAdd(entity);
        await Context.AddAsync(entity, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<ICollection<TEntity>> AddRangeAsync(
        ICollection<TEntity> entities,
        CancellationToken cancellationToken = default
    )
    {
        foreach (TEntity entity in entities)
            EditEntityPropertiesToAdd(entity);
        await Context.AddRangeAsync(entities, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
        return entities;
    }

    protected virtual void EditEntityPropertiesToUpdate(TEntity entity)
    {
        entity.UpdatedDate = DateTime.UtcNow;
        if (entity is IEntityAudit auditOnUpdate)
            auditOnUpdate.UpdatedById = CurrentUserId;
    }

    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        GuardValidId(entity);
        GuardTenantOwnership(entity);
        EditEntityPropertiesToUpdate(entity);
        Context.Update(entity);
        await Context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<ICollection<TEntity>> UpdateRangeAsync(
        ICollection<TEntity> entities,
        CancellationToken cancellationToken = default
    )
    {
        GuardValidId(entities);
        GuardTenantOwnership(entities);
        foreach (TEntity entity in entities)
            EditEntityPropertiesToUpdate(entity);
        Context.UpdateRange(entities);
        await Context.SaveChangesAsync(cancellationToken);
        return entities;
    }

    public async Task<TEntity> DeleteAsync(TEntity entity, bool permanent = false, CancellationToken cancellationToken = default)
    {
        GuardValidId(entity);
        GuardTenantOwnership(entity);
        await SetEntityAsDeleted(entity, permanent, isAsync: true, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<ICollection<TEntity>> DeleteRangeAsync(
        ICollection<TEntity> entities,
        bool permanent = false,
        CancellationToken cancellationToken = default
    )
    {
        GuardValidId(entities);
        GuardTenantOwnership(entities);
        await SetEntityAsDeleted(entities, permanent, isAsync: true, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
        return entities;
    }

    public async Task<IPaginate<TEntity>> GetListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<TEntity> queryable = Query();
        if (!enableTracking)
            queryable = queryable.AsNoTracking();
        if (include != null)
            queryable = include(queryable);
        if (withDeleted)
            queryable = ApplyIncludeDeleted(queryable);
        if (predicate != null)
            queryable = queryable.Where(predicate);
        if (orderBy != null)
            return await orderBy(queryable).ToPaginateAsync(index, size, from: 0, cancellationToken);
        return await queryable.ToPaginateAsync(index, size, from: 0, cancellationToken);
    }

    public async Task<TEntity?> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<TEntity> queryable = Query();
        if (!enableTracking)
            queryable = queryable.AsNoTracking();
        if (include != null)
            queryable = include(queryable);
        if (withDeleted)
            queryable = ApplyIncludeDeleted(queryable);
        return await queryable.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<IPaginate<TEntity>> GetListByDynamicAsync(
        DynamicQuery dynamic,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withDeleted = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<TEntity> queryable = Query().ToDynamic(dynamic);
        if (!enableTracking)
            queryable = queryable.AsNoTracking();
        if (include != null)
            queryable = include(queryable);
        if (withDeleted)
            queryable = ApplyIncludeDeleted(queryable);
        if (predicate != null)
            queryable = queryable.Where(predicate);
        return await queryable.ToPaginateAsync(index, size, from: 0, cancellationToken);
    }

    public async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool withDeleted = false,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<TEntity> queryable = Query();
        if (include != null)
            queryable = include(queryable);
        if (withDeleted)
            queryable = ApplyIncludeDeleted(queryable);
        if (predicate != null)
            queryable = queryable.Where(predicate);
        return await queryable.AnyAsync(cancellationToken);
    }

    public TEntity Add(TEntity entity)
    {
        EditEntityPropertiesToAdd(entity);
        Context.Add(entity);
        Context.SaveChanges();
        return entity;
    }

    public ICollection<TEntity> AddRange(ICollection<TEntity> entities)
    {
        foreach (TEntity entity in entities)
            EditEntityPropertiesToAdd(entity);
        Context.AddRange(entities);
        Context.SaveChanges();
        return entities;
    }

    public TEntity Update(TEntity entity)
    {
        GuardValidId(entity);
        GuardTenantOwnership(entity);
        EditEntityPropertiesToUpdate(entity);
        Context.Update(entity);
        Context.SaveChanges();
        return entity;
    }

    public ICollection<TEntity> UpdateRange(ICollection<TEntity> entities)
    {
        GuardValidId(entities);
        GuardTenantOwnership(entities);
        foreach (TEntity entity in entities)
            EditEntityPropertiesToUpdate(entity);
        Context.UpdateRange(entities);
        Context.SaveChanges();
        return entities;
    }

    public TEntity Delete(TEntity entity, bool permanent = false)
    {
        GuardValidId(entity);
        GuardTenantOwnership(entity);
        // .Wait() wraps thrown exceptions in AggregateException, obscuring the originals.
        // .GetAwaiter().GetResult() preserves the original exception type. The async path
        // is invoked with isAsync:false, so no real I/O occurs synchronously.
        SetEntityAsDeleted(entity, permanent, isAsync: false).GetAwaiter().GetResult();
        Context.SaveChanges();
        return entity;
    }

    public ICollection<TEntity> DeleteRange(ICollection<TEntity> entities, bool permanent = false)
    {
        GuardValidId(entities);
        GuardTenantOwnership(entities);
        SetEntityAsDeleted(entities, permanent, isAsync: false).GetAwaiter().GetResult();
        Context.SaveChanges();
        return entities;
    }

    public TEntity? Get(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool withDeleted = false,
        bool enableTracking = true
    )
    {
        IQueryable<TEntity> queryable = Query();
        if (!enableTracking)
            queryable = queryable.AsNoTracking();
        if (include != null)
            queryable = include(queryable);
        if (withDeleted)
            queryable = ApplyIncludeDeleted(queryable);
        return queryable.FirstOrDefault(predicate);
    }

    public IPaginate<TEntity> GetList(
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withDeleted = false,
        bool enableTracking = true
    )
    {
        IQueryable<TEntity> queryable = Query();
        if (!enableTracking)
            queryable = queryable.AsNoTracking();
        if (include != null)
            queryable = include(queryable);
        if (withDeleted)
            queryable = ApplyIncludeDeleted(queryable);
        if (predicate != null)
            queryable = queryable.Where(predicate);
        if (orderBy != null)
            return orderBy(queryable).ToPaginate(index, size, from: 0);
        return queryable.ToPaginate(index, size, from: 0);
    }

    public IPaginate<TEntity> GetListByDynamic(
        DynamicQuery dynamic,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withDeleted = false,
        bool enableTracking = true
    )
    {
        IQueryable<TEntity> queryable = Query().ToDynamic(dynamic);
        if (!enableTracking)
            queryable = queryable.AsNoTracking();
        if (include != null)
            queryable = include(queryable);
        if (withDeleted)
            queryable = ApplyIncludeDeleted(queryable);
        if (predicate != null)
            queryable = queryable.Where(predicate);
        return queryable.ToPaginate(index, size, from: 0);
    }

    public bool Any(
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool withDeleted = false
    )
    {
        IQueryable<TEntity> queryable = Query();
        if (include != null)
            queryable = include(queryable);
        if (withDeleted)
            queryable = ApplyIncludeDeleted(queryable);
        if (predicate != null)
            queryable = queryable.Where(predicate);
        return queryable.Any();
    }

    protected async Task SetEntityAsDeleted(
        TEntity entity,
        bool permanent,
        bool isAsync = true,
        CancellationToken cancellationToken = default
    )
    {
        if (!permanent)
        {
            CheckHasEntityHaveOneToOneRelation(entity);
            if (isAsync)
                await setEntityAsSoftDeleted(entity, isAsync, cancellationToken);
            else
                setEntityAsSoftDeleted(entity, isAsync).Wait();
        }
        else
            Context.Remove(entity);
    }

    protected async Task SetEntityAsDeleted(
        IEnumerable<TEntity> entities,
        bool permanent,
        bool isAsync = true,
        CancellationToken cancellationToken = default
    )
    {
        foreach (TEntity entity in entities)
            await SetEntityAsDeleted(entity, permanent, isAsync, cancellationToken);
    }

    protected IQueryable<object>? GetRelationLoaderQuery(IQueryable query, Type navigationPropertyType)
    {
        Type queryProviderType = query.Provider.GetType();
        MethodInfo createQueryMethod =
            queryProviderType
                .GetMethods()
                .First(m => m is { Name: nameof(query.Provider.CreateQuery), IsGenericMethod: true })
                ?.MakeGenericMethod(navigationPropertyType)
            ?? throw new InvalidOperationException("CreateQuery<TElement> method is not found in IQueryProvider.");
        var queryProviderQuery = (IQueryable<object>)createQueryMethod.Invoke(query.Provider, parameters: [query.Expression])!;
        queryProviderQuery = queryProviderQuery.Where(x => !((IEntityTimestamps)x).DeletedDate.HasValue);

        // EF Core global query filter is not guaranteed to apply when cascading soft-deletes via
        // a manually built IQueryable on a relation. Explicitly enforce tenant ownership here so
        // a cascade can never touch another tenant's row (defense-in-depth with the global filter).
        if (typeof(ITenantEntity).IsAssignableFrom(navigationPropertyType)
            && TenantSetter is { IsSuperAdmin: false, CurrentTenantId: { } tenantId })
        {
            queryProviderQuery = queryProviderQuery.Where(x => ((ITenantEntity)x).TenantId == tenantId);
        }

        return queryProviderQuery;
    }

    protected void CheckHasEntityHaveOneToOneRelation(TEntity entity)
    {
        IEnumerable<IForeignKey> foreignKeys = Context.Entry(entity).Metadata.GetForeignKeys();
        IForeignKey? oneToOneForeignKey = foreignKeys.FirstOrDefault(fk =>
            fk.IsUnique && fk.PrincipalKey.Properties.All(pk => Context.Entry(entity).Property(pk.Name).Metadata.IsPrimaryKey())
        );

        if (oneToOneForeignKey != null)
        {
            string relatedEntity = oneToOneForeignKey.PrincipalEntityType.ClrType.Name;
            IReadOnlyList<IProperty> primaryKeyProperties = Context.Entry(entity).Metadata.FindPrimaryKey()!.Properties;
            string primaryKeyNames = string.Join(", ", primaryKeyProperties.Select(prop => prop.Name));
            throw new InvalidOperationException(
                $"Entity {entity.GetType().Name} has a one-to-one relationship with {relatedEntity} via the primary key ({primaryKeyNames}). Soft Delete causes problems if you try to create an entry again with the same foreign key."
            );
        }
    }

    protected virtual void EditEntityPropertiesToDelete(TEntity entity)
    {
        entity.DeletedDate = DateTime.UtcNow;
        if (entity is IEntityAudit auditOnDelete)
            auditOnDelete.DeletedById = CurrentUserId;
    }

    protected virtual void EditRelationEntityPropertiesToCascadeSoftDelete(IEntityTimestamps entity)
    {
        entity.DeletedDate = DateTime.UtcNow;
        if (entity is IEntityAudit auditEntity)
            auditEntity.DeletedById = CurrentUserId;
    }

    protected virtual bool IsSoftDeleted(IEntityTimestamps entity)
    {
        return entity.DeletedDate.HasValue;
    }

    private async Task setEntityAsSoftDeleted(
        IEntityTimestamps entity,
        bool isAsync = true,
        CancellationToken cancellationToken = default,
        bool isRoot = true,
        HashSet<object>? visited = null
    )
    {
        // Cycle protection: navigation graphs can form back-references (A → B → A).
        // Without a visited set the recursion can stack-overflow on real graphs.
        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!visited.Add(entity))
            return;

        if (IsSoftDeleted(entity))
            return;
        if (isRoot)
            EditEntityPropertiesToDelete((TEntity)entity);
        else
            EditRelationEntityPropertiesToCascadeSoftDelete(entity);

        var navigations = Context
            .Entry(entity)
            .Metadata.GetNavigations()
            .Where(x =>
                x is { IsOnDependent: false, ForeignKey.DeleteBehavior: DeleteBehavior.ClientCascade or DeleteBehavior.Cascade }
            )
            .ToList();
        foreach (INavigation? navigation in navigations)
        {
            if (navigation.TargetEntityType.IsOwned())
                continue;
            if (navigation.PropertyInfo == null)
                continue;

            object? navValue = navigation.PropertyInfo.GetValue(entity);
            if (navigation.IsCollection)
            {
                if (navValue == null)
                {
                    IQueryable query = Context.Entry(entity).Collection(navigation.PropertyInfo.Name).Query();

                    if (isAsync)
                    {
                        IQueryable<object>? relationLoaderQuery = GetRelationLoaderQuery(
                            query,
                            navigationPropertyType: navigation.TargetEntityType.ClrType
                        );
                        if (relationLoaderQuery is not null)
                            navValue = await relationLoaderQuery.ToListAsync(cancellationToken);
                    }
                    else
                        navValue = GetRelationLoaderQuery(query, navigationPropertyType: navigation.TargetEntityType.ClrType)
                            ?.ToList();

                    if (navValue == null)
                        continue;
                }

                foreach (object navValueItem in (IEnumerable)navValue)
                    await setEntityAsSoftDeleted((IEntityTimestamps)navValueItem, isAsync, cancellationToken, isRoot: false, visited);
            }
            else
            {
                if (navValue == null)
                {
                    IQueryable query = Context.Entry(entity).Reference(navigation.PropertyInfo.Name).Query();

                    if (isAsync)
                    {
                        IQueryable<object>? relationLoaderQuery = GetRelationLoaderQuery(
                            query,
                            navigationPropertyType: navigation.TargetEntityType.ClrType
                        );
                        if (relationLoaderQuery is not null)
                            navValue = await relationLoaderQuery.FirstOrDefaultAsync(cancellationToken);
                    }
                    else
                        navValue = GetRelationLoaderQuery(query, navigationPropertyType: navigation.TargetEntityType.ClrType)
                            ?.FirstOrDefault();

                    if (navValue == null)
                        continue;
                }

                await setEntityAsSoftDeleted((IEntityTimestamps)navValue, isAsync, cancellationToken, isRoot: false, visited);
            }
        }

        Context.Update(entity);
    }
}
