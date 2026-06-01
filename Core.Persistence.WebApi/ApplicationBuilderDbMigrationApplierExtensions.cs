using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Persistence.DbMigrationApplier;

namespace NetCoreBackend.NArchitecture.Core.Persistence.WebApi;

public static class ApplicationBuilderDbMigrationApplierExtensions
{
    public static IApplicationBuilder UseDbMigrationApplier(this IApplicationBuilder app)
    {
        // DbContext is typically registered as Scoped. Resolving IDbMigrationApplierService
        // directly from app.ApplicationServices (root container) would throw on a scoped
        // DbContext dependency. Open a startup scope so migrations run in a proper Scoped
        // context that is disposed when the loop finishes.
        using IServiceScope scope = app.ApplicationServices.CreateScope();
        IEnumerable<IDbMigrationApplierService> migrationCreatorServices =
            scope.ServiceProvider.GetServices<IDbMigrationApplierService>();
        foreach (IDbMigrationApplierService service in migrationCreatorServices)
            service.Initialize();

        return app;
    }
}
