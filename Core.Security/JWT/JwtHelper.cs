using System.Collections.Immutable;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using NetCoreBackend.NArchitecture.Core.Security.Encryption;
using NetCoreBackend.NArchitecture.Core.Security.Entities;
using NetCoreBackend.NArchitecture.Core.Security.Extensions;

namespace NetCoreBackend.NArchitecture.Core.Security.JWT;

public class JwtHelper<TUserId, TOperationClaimId, TRefreshTokenId> : ITokenHelper<TUserId, TOperationClaimId, TRefreshTokenId>
{
    private readonly TokenOptions _tokenOptions;

    public JwtHelper(TokenOptions tokenOptions)
    {
        _tokenOptions = tokenOptions;
    }

    public virtual AccessToken CreateToken(User<TUserId> user, IList<OperationClaim<TOperationClaimId>> operationClaims)
    {
        return BuildAccessToken(SetClaims(user, operationClaims));
    }

    public RefreshToken<TRefreshTokenId, TUserId> CreateRefreshToken(User<TUserId> user, string ipAddress)
    {
        return new RefreshToken<TRefreshTokenId, TUserId>()
        {
            UserId = user.Id,
            Token = RandomRefreshToken(),
            ExpirationDate = DateTime.UtcNow.AddDays(_tokenOptions.RefreshTokenTtlDays),
            CreatedByIp = ipAddress
        };
    }

    public virtual AccessToken CreateAdminToken(
        PlatformAdmin<TUserId> admin,
        IList<OperationClaim<TOperationClaimId>> operationClaims)
    {
        return BuildAccessToken(SetAdminClaims(admin, operationClaims, tenantId: null, isImpersonating: false));
    }

    public virtual AccessToken CreateImpersonationToken(
        PlatformAdmin<TUserId> admin,
        IList<OperationClaim<TOperationClaimId>> operationClaims,
        Guid tenantId)
    {
        return BuildAccessToken(SetAdminClaims(admin, operationClaims, tenantId, isImpersonating: true));
    }

    protected virtual IEnumerable<Claim> SetClaims(
        User<TUserId> user,
        IList<OperationClaim<TOperationClaimId>> operationClaims)
    {
        List<Claim> claims = [];
        claims.AddNameIdentifier(user.Id!.ToString()!);
        claims.AddEmail(user.Email);
        claims.AddRoles(operationClaims.Select(c => c.Name).ToArray());
        claims.AddTenantId(user.TenantId);
        claims.AddIsSuperAdmin(false);
        claims.AddIsImpersonating(false);
        return claims.ToImmutableList();
    }

    protected virtual IEnumerable<Claim> SetAdminClaims(
        PlatformAdmin<TUserId> admin,
        IList<OperationClaim<TOperationClaimId>> operationClaims,
        Guid? tenantId,
        bool isImpersonating)
    {
        List<Claim> claims = [];
        claims.AddNameIdentifier(admin.Id!.ToString()!);
        claims.AddEmail(admin.Email);
        claims.AddRoles(operationClaims.Select(c => c.Name).ToArray());
        claims.AddTenantId(tenantId);
        claims.AddIsSuperAdmin(true);
        claims.AddIsImpersonating(isImpersonating);
        return claims.ToImmutableList();
    }

    private AccessToken BuildAccessToken(IEnumerable<Claim> claims)
    {
        // JWT spec ("exp", "nbf") uses Unix epoch (UTC). Using DateTime.Now would shift token lifetime
        // by the server timezone offset, causing inconsistent expiration across timezones.
        DateTime notBefore = DateTime.UtcNow;
        DateTime expiration = notBefore.AddMinutes(_tokenOptions.AccessTokenExpiration);
        SecurityKey securityKey = SecurityKeyHelper.CreateSecurityKey(_tokenOptions.SecurityKey);
        SigningCredentials signingCredentials = SigningCredentialsHelper.CreateSigningCredentials(securityKey);
        JwtSecurityToken jwt = new(
            _tokenOptions.Issuer,
            _tokenOptions.Audience,
            expires: expiration,
            notBefore: notBefore,
            claims: claims,
            signingCredentials: signingCredentials
        );
        return new AccessToken { Token = new JwtSecurityTokenHandler().WriteToken(jwt), ExpirationDate = expiration };
    }

    private string RandomRefreshToken()
    {
        byte[] numberByte = new byte[32];
        using var random = RandomNumberGenerator.Create();
        random.GetBytes(numberByte);
        return Convert.ToBase64String(numberByte);
    }
}
