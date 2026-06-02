using NetCoreBackend.NArchitecture.Core.Security.Entities;

namespace NetCoreBackend.NArchitecture.Core.Security.JWT;

/// <summary>
/// Stateless helpers for the refresh-token rotation + reuse-detection protocol.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Rotation:</strong> every refresh hands out a NEW refresh token and revokes the
/// old one (linking them via <c>ReplacedByToken</c>). A stolen token becomes unusable as
/// soon as the legitimate client refreshes once.
/// </para>
/// <para>
/// <strong>Reuse detection:</strong> if a request presents a refresh token that is already
/// revoked, that's evidence either the attacker or the legitimate user is replaying. The
/// framework cannot tell which, so it revokes the ENTIRE chain of refresh tokens for that
/// user (the "family") — forcing a fresh login and invalidating any token the attacker
/// still holds.
/// </para>
/// <para>
/// This class contains only the in-memory rules. Persistence (lookup by token value, fetch
/// family, save changes) is the consuming app's responsibility — it owns the
/// <c>DbContext</c>.
/// </para>
/// </remarks>
public static class RefreshTokenRotation
{
    /// <summary>Reason text written to <c>RefreshToken.ReasonRevoked</c> when a token is
    /// retired as part of a normal user-initiated refresh.</summary>
    public const string ReasonRotated = "Rotated by user refresh.";

    /// <summary>Reason text written to <c>RefreshToken.ReasonRevoked</c> when a family is
    /// wiped because a previously-rotated token was replayed.</summary>
    public const string ReasonReuseDetected = "Refresh token reuse detected — family revoked.";

    /// <summary>
    /// Mark <paramref name="token"/> as revoked because it was just rotated and record the
    /// link to its <paramref name="replacement"/>. Caller is responsible for persisting both
    /// rows (the old one updated, the replacement inserted) within the refresh handler's
    /// transaction.
    /// </summary>
    /// <param name="token">Token being retired.</param>
    /// <param name="replacement">Newly minted token that supersedes <paramref name="token"/>.</param>
    /// <param name="revokedByIp">Caller IP — recorded for forensic audit.</param>
    public static void Rotate<TId, TUserId>(
        RefreshToken<TId, TUserId> token,
        RefreshToken<TId, TUserId> replacement,
        string revokedByIp)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(replacement);
        token.RevokedDate = DateTime.UtcNow;
        token.RevokedByIp = revokedByIp;
        token.ReplacedByToken = replacement.Token;
        token.ReasonRevoked = ReasonRotated;
    }

    /// <summary>
    /// Detect reuse of a previously-rotated <paramref name="presented"/> token and revoke
    /// the entire <paramref name="family"/> when triggered. The caller must reject the
    /// refresh attempt AND persist the family-wide revocation.
    /// </summary>
    /// <param name="presented">The refresh token the caller just submitted.</param>
    /// <param name="family">All refresh tokens belonging to the same user (typically
    /// fetched as <c>repo.Where(t =&gt; t.UserId == presented.UserId)</c>).</param>
    /// <param name="revokedByIp">Caller IP of the suspicious request — stamped on every
    /// freshly-revoked row AND on <paramref name="presented"/> (overwriting its original
    /// rotation IP) so the audit trail records who triggered the family wipe.</param>
    /// <returns><c>true</c> when reuse was detected and the family was revoked;
    /// <c>false</c> when <paramref name="presented"/> is still active and no action was
    /// taken.</returns>
    public static bool DetectReuseAndRevokeFamily<TId, TUserId>(
        RefreshToken<TId, TUserId> presented,
        IEnumerable<RefreshToken<TId, TUserId>> family,
        string revokedByIp)
    {
        ArgumentNullException.ThrowIfNull(presented);
        ArgumentNullException.ThrowIfNull(family);
        if (!presented.IsRevoked) return false;

        DateTime now = DateTime.UtcNow;
        foreach (RefreshToken<TId, TUserId> token in family)
        {
            if (token.IsRevoked) continue;
            token.RevokedDate = now;
            token.RevokedByIp = revokedByIp;
            token.ReasonRevoked = ReasonReuseDetected;
        }

        // Presented token enters this branch as already-revoked (rotation marked it earlier)
        // and the family loop skips it. Overwrite its revocation metadata so the audit trail
        // tells the truth: the row that triggered the family wipe is itself marked with the
        // ReuseDetected reason, not its original "Rotated" reason.
        presented.ReasonRevoked = ReasonReuseDetected;
        presented.RevokedByIp = revokedByIp;
        return true;
    }
}
