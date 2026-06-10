using NetCoreBackend.NArchitecture.Core.Persistence.Dynamic;
using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Security.Entities;

// Platform-level administrator — not scoped to any tenant.
// Stored in a separate table from tenant User records.
// Use Entity<TId> (not TenantEntity) so no TenantId column is created
// and EF Core global query filters never apply to this table.
public class PlatformAdmin<TId> : AuditableEntity<TId>
{
    public string Email { get; set; }
    [NotFilterable] public byte[] PasswordSalt { get; set; }
    [NotFilterable] public byte[] PasswordHash { get; set; }

    public PlatformAdmin()
    {
        Email = string.Empty;
        PasswordSalt = Array.Empty<byte>();
        PasswordHash = Array.Empty<byte>();
    }

    public PlatformAdmin(string email, byte[] passwordSalt, byte[] passwordHash)
    {
        Email = email;
        PasswordSalt = passwordSalt;
        PasswordHash = passwordHash;
    }

    public PlatformAdmin(TId id, string email, byte[] passwordSalt, byte[] passwordHash)
        : base(id)
    {
        Email = email;
        PasswordSalt = passwordSalt;
        PasswordHash = passwordHash;
    }
}
