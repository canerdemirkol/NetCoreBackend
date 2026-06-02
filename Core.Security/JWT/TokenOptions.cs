using System.Text;

namespace NetCoreBackend.NArchitecture.Core.Security.JWT;

public class TokenOptions
{
    // HS256 minimum: 256 bits = 32 bytes. JwtBearer will throw IDX10720 with a confusing
    // message if the configured key is shorter; we fail fast at startup with a clear cause.
    private const int _minSecurityKeyBytes = 32;

    public string Audience { get; set; }
    public string Issuer { get; set; }

    /// <summary>
    /// Access token expiration time in minutes.
    /// </summary>
    public int AccessTokenExpiration { get; set; }

    public string SecurityKey { get; set; }

    /// <summary>
    /// Refresh token lifetime in days. Renamed from RefreshTokenTTL so JSON config binding
    /// matches the documented key ("RefreshTokenTtlDays") — case-insensitive deserialization
    /// is still name-sensitive, and the previous mismatch silently bound 0 → tokens expired immediately.
    /// </summary>
    public int RefreshTokenTtlDays { get; set; }

    public TokenOptions()
    {
        Audience = string.Empty;
        Issuer = string.Empty;
        SecurityKey = string.Empty;
    }

    public TokenOptions(string audience, string issuer, int accessTokenExpiration, string securityKey, int refreshTokenTtlDays)
    {
        Audience = audience;
        Issuer = issuer;
        AccessTokenExpiration = accessTokenExpiration;
        SecurityKey = securityKey;
        RefreshTokenTtlDays = refreshTokenTtlDays;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("TokenOptions.Audience is required (typically the API host).");
        if (string.IsNullOrWhiteSpace(Issuer))
            throw new InvalidOperationException("TokenOptions.Issuer is required (typically the identity authority).");
        if (string.IsNullOrWhiteSpace(SecurityKey))
            throw new InvalidOperationException("TokenOptions.SecurityKey is required.");
        if (Encoding.UTF8.GetByteCount(SecurityKey) < _minSecurityKeyBytes)
            throw new InvalidOperationException(
                $"TokenOptions.SecurityKey is too short ({Encoding.UTF8.GetByteCount(SecurityKey)} bytes); minimum {_minSecurityKeyBytes} bytes for HS256.");
        if (AccessTokenExpiration <= 0)
            throw new InvalidOperationException("TokenOptions.AccessTokenExpiration must be a positive number of minutes.");
        if (RefreshTokenTtlDays <= 0)
            throw new InvalidOperationException("TokenOptions.RefreshTokenTtlDays must be a positive number of days.");
    }
}
