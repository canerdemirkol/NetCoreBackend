using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace NetCoreBackend.NArchitecture.Core.Persistence.DbMigrationApplier;

public static class DatabaseFacadeDbMigrationApplierExtensions
{
    public static DatabaseFacade EnsureDbApplied(this DatabaseFacade databaseFacade, ILogger? logger = null)
    {
        // Reaching the DB is a prerequisite — but a false here can mean "DB exists but creds
        // are wrong" just as easily as "DB doesn't exist yet". Log it so startup doesn't go
        // silent when migrations are being skipped.
        if (!databaseFacade.CanConnect())
        {
            logger?.LogWarning(
                "EnsureDbApplied: CanConnect() returned false. Skipping migrations. " +
                "Check connection string, network reachability, and credentials.");
            return databaseFacade;
        }

        if (databaseFacade.IsInMemory())
        {
            logger?.LogInformation("EnsureDbApplied: InMemory provider — calling EnsureCreated.");
            _ = databaseFacade.EnsureCreated();
            return databaseFacade;
        }

        if (databaseFacade.IsRelational())
        {
            try
            {
                IEnumerable<string> pending = databaseFacade.GetPendingMigrations();
                int pendingCount = pending is ICollection<string> c ? c.Count : pending.Count();
                if (pendingCount == 0)
                {
                    logger?.LogInformation("EnsureDbApplied: no pending migrations.");
                    return databaseFacade;
                }

                logger?.LogInformation("EnsureDbApplied: applying {Count} pending migration(s).", pendingCount);
                databaseFacade.Migrate();
                logger?.LogInformation("EnsureDbApplied: migrations applied successfully.");
            }
            catch (Exception ex)
            {
                // Wrap so the consuming host's exception logger gets a self-contained message
                // — bare EF Core exceptions often surface as opaque schema/lock errors at
                // startup, costing operators time to root-cause.
                logger?.LogError(ex, "EnsureDbApplied: failed to apply migrations.");
                throw new InvalidOperationException(
                    "Database migration failed. See inner exception for the EF Core error " +
                    "(common causes: schema lock from a concurrent deployment, missing " +
                    "permissions, or a destructive migration on a non-empty table).", ex);
            }
        }

        return databaseFacade;
    }
}
