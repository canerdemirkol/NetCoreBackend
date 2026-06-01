using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Persistence.DbMigrationApplier;

namespace NetCoreBackend.NArchitecture.Core.Persistence.DependencyInjection;

public static class ServiceCollectionDbMigrationApplierExtensions
{
    // The previous implementation called services.BuildServiceProvider() at registration time —
    // an ASP0000 anti-pattern that creates a parallel service tree, leaks the temp provider
    // (never disposed), and uses a DbContext detached from the runtime DI graph. The factories
    // below resolve TDbContext from the real ServiceProvider at the time UseDbMigrationApplier()
    // is invoked, so migrations run against the app's actual context with the right lifetime.
    public static IServiceCollection AddDbMigrationApplier<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddTransient<IDbMigrationApplierService>(sp =>
            new DbMigrationApplierManager<TDbContext>(sp.GetRequiredService<TDbContext>()));
        services.AddTransient<IDbMigrationApplierService<TDbContext>>(sp =>
            new DbMigrationApplierManager<TDbContext>(sp.GetRequiredService<TDbContext>()));

        return services;
    }
}
