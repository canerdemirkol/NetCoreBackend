using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Security.Entities;

// Refresh token for PlatformAdmin. Extends Entity (not TenantEntity) because
// platform admins are not scoped to any tenant.
public class AdminRefreshToken<TId, TAdminId> : Entity<TId>
{
    public TAdminId AdminId { get; set; }
    public string Token { get; set; }
    public DateTime ExpirationDate { get; set; }
    public string CreatedByIp { get; set; }
    public DateTime? RevokedDate { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? ReasonRevoked { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpirationDate;
    public bool IsRevoked => RevokedDate.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;

    public AdminRefreshToken()
    {
        AdminId = default!;
        Token = string.Empty;
        CreatedByIp = string.Empty;
    }

    public AdminRefreshToken(TAdminId adminId, string token, DateTime expirationDate, string createdByIp)
    {
        AdminId = adminId;
        Token = token;
        ExpirationDate = expirationDate;
        CreatedByIp = createdByIp;
    }

    public AdminRefreshToken(TId id, TAdminId adminId, string token, DateTime expirationDate, string createdByIp)
        : base(id)
    {
        AdminId = adminId;
        Token = token;
        ExpirationDate = expirationDate;
        CreatedByIp = createdByIp;
    }
}
