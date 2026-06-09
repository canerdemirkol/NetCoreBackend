using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;
using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.MultiTenancy.Extensions;

/// <summary>
/// Applies tenant isolation global query filters to all ITenantEntity types in one call.
/// Invoke from DbContext.OnModelCreating: builder.ApplyTenantFilters(_tenantContext);
/// </summary>
public static class ModelBuilderTenantExtensions
{
    public static ModelBuilder ApplyTenantFilters(this ModelBuilder builder, ITenantContext tenantContext)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProperty = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
            var tenantContextConst = Expression.Constant(tenantContext);

            var isSuperAdmin = Expression.Property(tenantContextConst, nameof(ITenantContext.IsSuperAdmin));

            // Null-safe: guard .Value with .HasValue so requests without a tenant context
            // return an empty set instead of throwing InvalidOperationException at query time.
            var tenantIdNullable = Expression.Property(tenantContextConst, nameof(ITenantContext.TenantId));
            var hasValue = Expression.Property(tenantIdNullable, nameof(Nullable<Guid>.HasValue));
            var tenantIdValue = Expression.Property(tenantIdNullable, nameof(Nullable<Guid>.Value));
            var tenantMatch = Expression.AndAlso(hasValue, Expression.Equal(tenantIdProperty, tenantIdValue));

            var filter = Expression.OrElse(isSuperAdmin, tenantMatch);
            builder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(filter, parameter));
        }

        return builder;
    }
}
