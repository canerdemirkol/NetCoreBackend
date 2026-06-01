namespace NetCoreBackend.NArchitecture.Core.Security.JWT;

public class TokenOptions
{
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
}
