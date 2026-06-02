using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Security.Entities;

public class RefreshToken<TId, TUserId> : TenantEntity<TId>
{
    public TUserId UserId { get; set; }
    public string Token { get; set; }

    // ExpirationDate is always UTC. Comparing against DateTime.Now would mis-fire by the
    // server's local-time offset on first deploy to a non-UTC region.
    public DateTime ExpirationDate { get; set; }
    public string CreatedByIp { get; set; }
    public DateTime? RevokedDate { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? ReasonRevoked { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpirationDate;
    public bool IsRevoked => RevokedDate.HasValue;
    // A token is "active" only when it is neither expired nor revoked. A token presented
    // for refresh that fails this check is the canonical theft signal — see
    // RefreshTokenRotation for the recommended response.
    public bool IsActive => !IsRevoked && !IsExpired;

    public RefreshToken()
    {
        UserId = default!;
        Token = string.Empty;
        CreatedByIp = string.Empty;
    }

    public RefreshToken(TUserId userId, string token, DateTime expirationDate, string createdByIp)
    {
        UserId = userId;
        Token = token;
        ExpirationDate = expirationDate;
        CreatedByIp = createdByIp;
    }

    public RefreshToken(TId id, TUserId userId, string token, DateTime expirationDate, string createdByIp)
        : base(id)
    {
        UserId = userId;
        Token = token;
        ExpirationDate = expirationDate;
        CreatedByIp = createdByIp;
    }
}
