using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NetCoreBackend.NArchitecture.Core.Persistence.DbMigrationApplier;

public class DbMigrationApplierManager<TDbContext> : IDbMigrationApplierService<TDbContext>
    where TDbContext : DbContext
{
    private readonly TDbContext _context;
    private readonly ILogger<DbMigrationApplierManager<TDbContext>>? _logger;

    public DbMigrationApplierManager(TDbContext context, ILogger<DbMigrationApplierManager<TDbContext>>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    public void Initialize()
    {
        _context.Database.EnsureDbApplied(_logger);
    }
}
