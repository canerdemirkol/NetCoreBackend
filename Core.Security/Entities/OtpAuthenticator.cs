using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Security.Entities;

public class OtpAuthenticator<TUserId> : TenantEntity<TUserId>
{
    public TUserId UserId { get; set; }
    public byte[] SecretKey { get; set; }
    public bool IsVerified { get; set; }

    public OtpAuthenticator()
    {
        UserId = default!;
        SecretKey = Array.Empty<byte>();
    }

    public OtpAuthenticator(TUserId userId, byte[] secretKey, bool isVerified)
    {
        UserId = userId;
        SecretKey = secretKey;
        IsVerified = isVerified;
    }

    public OtpAuthenticator(TUserId id, TUserId userId, byte[] secretKey, bool isVerified)
        : base(id)
    {
        UserId = userId;
        SecretKey = secretKey;
        IsVerified = isVerified;
    }
}
