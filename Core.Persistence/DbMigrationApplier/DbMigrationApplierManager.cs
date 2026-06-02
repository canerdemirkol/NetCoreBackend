using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NetCoreBackend.NArchitecture.Core.Persistence.DbMigrationApplier;

public class DbMigrationApplierManager<TDbContext> : IDbMigrationApplierService<TDbContext>
    where TDbContext : DbContext
{
    private readonly TDbContext _context;
    private readonly ILogger<DbMigrationApplierManager<TDbContext>> _logger;

    // ILogger<T> is always satisfiable from the .NET generic host's logging factory (even
    // when no sink is wired up, NullLogger<T> is returned). Making it required removes the
    // "did the consumer remember to inject a logger?" question — startup migration logs are
    // the only signal operators get when migrations silently skip or fail.
    public DbMigrationApplierManager(TDbContext context, ILogger<DbMigrationApplierManager<TDbContext>> logger)
    {
        _context = context;
        _logger = logger;
    }

    public void Initialize()
    {
        _context.Database.EnsureDbApplied(_logger);
    }
}
