using NetCoreBackend.NArchitecture.Core.Persistence.Dynamic;
using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Security.Entities;

// Platform-level administrator. Not scoped to any tenant — no TenantId column,
// no EF Core global query filter. Stored separately from tenant User records.
public class PlatformAdmin<TId> : Entity<TId>
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
