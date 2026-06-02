using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Security.Entities;

/// <summary>
/// Email-based 2FA authenticator. Generic parameter <typeparamref name="TId"/> is the PK of this
/// entity; it is expected to be the same type as User's ID (typically Guid). Previously misnamed
/// "TUserId" which conflicted with the <see cref="UserId"/> FK semantics.
/// </summary>
public class EmailAuthenticator<TId> : TenantEntity<TId>
{
    public TId UserId { get; set; }

    // Activation key lifecycle:
    //   - Issued: ActivationKey is non-null, ActivationKeyExpiresAt is set in the future.
    //   - Consumed: ActivationKey nulled out and ActivationKeyConsumedAt stamped on first use.
    //   - Expired: ExpiresAt in the past — verification handlers must reject and require reissue.
    // Without ExpiresAt/ConsumedAt a leaked key could be redeemed indefinitely or replayed.
    public string? ActivationKey { get; set; }
    public DateTime? ActivationKeyExpiresAt { get; set; }
    public DateTime? ActivationKeyConsumedAt { get; set; }

    public bool IsVerified { get; set; }

    public EmailAuthenticator()
    {
        UserId = default!;
    }

    public EmailAuthenticator(TId userId, bool isVerified)
    {
        UserId = userId;
        IsVerified = isVerified;
    }

    public EmailAuthenticator(TId id, TId userId, bool isVerified)
        : base(id)
    {
        UserId = userId;
        IsVerified = isVerified;
    }
}
