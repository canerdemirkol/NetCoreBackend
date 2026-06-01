using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace NetCoreBackend.NArchitecture.Core.Security.Encryption;

public static class SecurityKeyHelper
{
    // HmacSha512 requires at least 64 bytes (512 bits) for full security; 32 bytes is the
    // NIST minimum for any HMAC. Reject shorter keys outright — short keys make JWT brute
    // force feasible against captured tokens.
    private const int MinKeyBytes = 32;

    public static SecurityKey CreateSecurityKey(string securityKey)
    {
        if (string.IsNullOrEmpty(securityKey))
            throw new ArgumentException("SecurityKey cannot be null or empty.", nameof(securityKey));

        byte[] keyBytes = Encoding.UTF8.GetBytes(securityKey);
        if (keyBytes.Length < MinKeyBytes)
            throw new ArgumentException(
                $"SecurityKey must be at least {MinKeyBytes} bytes ({MinKeyBytes * 8} bits) " +
                $"of UTF-8 to meet HMAC-SHA security requirements. Got {keyBytes.Length} bytes.",
                nameof(securityKey));

        return new SymmetricSecurityKey(keyBytes);
    }
}
