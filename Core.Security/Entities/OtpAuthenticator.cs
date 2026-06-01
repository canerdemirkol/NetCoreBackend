using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Security.Entities;

/// <summary>
/// TOTP authenticator record per user. <see cref="SecretKey"/> is the raw shared secret used to
/// compute one-time codes.
///
/// SECURITY: <see cref="SecretKey"/> is stored unencrypted in this entity. A DB leak exposes
/// every user's 2FA secret. Consuming applications SHOULD encrypt this column at rest:
///
/// <code>
/// modelBuilder.Entity&lt;OtpAuthenticator&lt;Guid&gt;&gt;()
///     .Property(e =&gt; e.SecretKey)
///     .HasConversion(
///         v =&gt; AesEncrypt(v, key),
///         v =&gt; AesDecrypt(v, key));
/// </code>
///
/// The encryption key should come from a KMS (Azure Key Vault, AWS KMS, HashiCorp Vault),
/// not the application config.
/// </summary>
/// <remarks>
/// Generic parameter <typeparamref name="TId"/> is the PK type of OtpAuthenticator itself
/// (typically the same as User's ID type — both Guid in most apps). It was previously
/// misnamed "TUserId" which conflicted with the <see cref="UserId"/> FK semantics.
/// </remarks>
public class OtpAuthenticator<TId> : TenantEntity<TId>
{
    public TId UserId { get; set; }
    public byte[] SecretKey { get; set; }
    public bool IsVerified { get; set; }

    public OtpAuthenticator()
    {
        UserId = default!;
        SecretKey = Array.Empty<byte>();
    }

    public OtpAuthenticator(TId userId, byte[] secretKey, bool isVerified)
    {
        UserId = userId;
        SecretKey = secretKey;
        IsVerified = isVerified;
    }

    public OtpAuthenticator(TId id, TId userId, byte[] secretKey, bool isVerified)
        : base(id)
    {
        UserId = userId;
        SecretKey = secretKey;
        IsVerified = isVerified;
    }
}
