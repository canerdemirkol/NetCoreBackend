using NetCoreBackend.NArchitecture.Core.Security.Entities;
using NetCoreBackend.NArchitecture.Core.Security.JWT;

namespace NetCoreBackend.NArchitecture.Core.Test.Security;

public sealed class RefreshTokenRotationTests
{
    private static RefreshToken<Guid, Guid> NewToken(string value, DateTime expiry) =>
        new(Guid.NewGuid(), Guid.NewGuid(), value, expiry, "127.0.0.1");

    [Fact]
    public void Rotate_RevokesOldAndLinksReplacement()
    {
        RefreshToken<Guid, Guid> old = NewToken("old-token", DateTime.UtcNow.AddDays(1));
        RefreshToken<Guid, Guid> replacement = NewToken("new-token", DateTime.UtcNow.AddDays(8));

        RefreshTokenRotation.Rotate(old, replacement, revokedByIp: "10.0.0.1");

        Assert.True(old.IsRevoked);
        Assert.Equal("10.0.0.1", old.RevokedByIp);
        Assert.Equal("new-token", old.ReplacedByToken);
        Assert.Equal(RefreshTokenRotation.ReasonRotated, old.ReasonRevoked);
        Assert.False(replacement.IsRevoked);
    }

    [Fact]
    public void DetectReuse_ActiveToken_DoesNothingAndReturnsFalse()
    {
        RefreshToken<Guid, Guid> activeToken = NewToken("live", DateTime.UtcNow.AddDays(1));
        RefreshToken<Guid, Guid>[] family = { activeToken, NewToken("sibling", DateTime.UtcNow.AddDays(1)) };

        bool detected = RefreshTokenRotation.DetectReuseAndRevokeFamily(activeToken, family, "10.0.0.1");

        Assert.False(detected);
        Assert.All(family, t => Assert.False(t.IsRevoked));
    }

    [Fact]
    public void DetectReuse_RevokedPresented_RevokesEntireFamilyAndOverwritesPresentedReason()
    {
        // Setup: presented is a rotated-out token (revoked with "Rotated" reason). When the
        // user replays it, framework should:
        //   1. Revoke EVERY active family member.
        //   2. Overwrite presented's reason from "Rotated" to "ReuseDetected" so the audit
        //      trail tells the truth about why the family was wiped.
        RefreshToken<Guid, Guid> presented = NewToken("stolen", DateTime.UtcNow.AddDays(1));
        RefreshToken<Guid, Guid> activeA = NewToken("active-a", DateTime.UtcNow.AddDays(1));
        RefreshToken<Guid, Guid> activeB = NewToken("active-b", DateTime.UtcNow.AddDays(1));

        // Simulate prior rotation that left "presented" as revoked-with-Rotated reason.
        RefreshTokenRotation.Rotate(presented, activeA, "192.168.1.1");
        Assert.Equal(RefreshTokenRotation.ReasonRotated, presented.ReasonRevoked);

        bool detected = RefreshTokenRotation.DetectReuseAndRevokeFamily(
            presented,
            new[] { presented, activeA, activeB },
            revokedByIp: "10.0.0.99");

        Assert.True(detected);
        // R4 fix #3 — presented gets its reason overwritten to ReuseDetected.
        Assert.Equal(RefreshTokenRotation.ReasonReuseDetected, presented.ReasonRevoked);
        Assert.Equal("10.0.0.99", presented.RevokedByIp);
        // Active family members revoked with the reuse-detected reason.
        Assert.True(activeA.IsRevoked);
        Assert.Equal(RefreshTokenRotation.ReasonReuseDetected, activeA.ReasonRevoked);
        Assert.True(activeB.IsRevoked);
        Assert.Equal(RefreshTokenRotation.ReasonReuseDetected, activeB.ReasonRevoked);
    }

    [Fact]
    public void IsExpired_RespectsUtcNow()
    {
        RefreshToken<Guid, Guid> expired = NewToken("x", DateTime.UtcNow.AddSeconds(-1));
        RefreshToken<Guid, Guid> future = NewToken("y", DateTime.UtcNow.AddDays(1));

        Assert.True(expired.IsExpired);
        Assert.False(future.IsExpired);
        Assert.True(future.IsActive);
        Assert.False(expired.IsActive);
    }
}
