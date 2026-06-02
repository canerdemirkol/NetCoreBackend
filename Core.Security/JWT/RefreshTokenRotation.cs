using NetCoreBackend.NArchitecture.Core.Security.Entities;

namespace NetCoreBackend.NArchitecture.Core.Security.JWT;

// Stateless helpers for the refresh-token rotation + reuse-detection protocol.
//
// Why rotation:
//   - Every refresh hands out a NEW refresh token and revokes the old one (linking them via
//     ReplacedByToken). A stolen token becomes unusable as soon as the legitimate client
//     refreshes once.
//
// Why reuse detection:
//   - If a request presents a refresh token that is already revoked, that's evidence either
//     the attacker or the legitimate user is replaying. We can't tell which side it was, so
//     we revoke the ENTIRE chain of refresh tokens for that user (the "family") — this
//     forces a fresh login and invalidates any token the attacker still holds.
//
// This file only contains the rules. Persistence (lookup by Token, fetch family, save changes)
// is the consuming app's responsibility — it owns the DbContext.
public static class RefreshTokenRotation
{
    public const string ReasonRotated = "Rotated by user refresh.";
    public const string ReasonReuseDetected = "Refresh token reuse detected — family revoked.";

    // Mark the given token as revoked because it was just rotated. The caller persists the
    // change and inserts `replacement` as a new row.
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

    // Returns true and marks every token in `family` as revoked when `presented` is already
    // revoked (i.e. the caller replayed a token that was previously rotated out). The caller
    // should reject the refresh attempt AND persist the family-wide revocation.
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
        return true;
    }
}
