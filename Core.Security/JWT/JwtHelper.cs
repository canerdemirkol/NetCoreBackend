using System.Collections.Immutable;
using System.Data;
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
        => CreateToken(user, operationClaims, tenantId: null, isSuperAdmin: false, isImpersonating: false);

    public virtual AccessToken CreateToken(
        User<TUserId> user,
        IList<OperationClaim<TOperationClaimId>> operationClaims,
        Guid? tenantId,
        bool isSuperAdmin = false,
        bool isImpersonating = false)
    {
        DateTime accessTokenExpiration = DateTime.Now.AddMinutes(_tokenOptions.AccessTokenExpiration);
        SecurityKey securityKey = SecurityKeyHelper.CreateSecurityKey(_tokenOptions.SecurityKey);
        SigningCredentials signingCredentials = SigningCredentialsHelper.CreateSigningCredentials(securityKey);
        JwtSecurityToken jwt = new JwtSecurityToken(
            _tokenOptions.Issuer,
            _tokenOptions.Audience,
            expires: accessTokenExpiration,
            notBefore: DateTime.Now,
            claims: SetClaims(user, operationClaims, tenantId, isSuperAdmin, isImpersonating),
            signingCredentials: signingCredentials
        );
        JwtSecurityTokenHandler jwtSecurityTokenHandler = new();
        string? token = jwtSecurityTokenHandler.WriteToken(jwt);

        return new AccessToken() { Token = token, ExpirationDate = accessTokenExpiration };
    }

    public RefreshToken<TRefreshTokenId, TUserId> CreateRefreshToken(User<TUserId> user, string ipAddress)
    {
        return new RefreshToken<TRefreshTokenId, TUserId>()
        {
            UserId = user.Id,
            Token = randomRefreshToken(),
            ExpirationDate = DateTime.UtcNow.AddDays(_tokenOptions.RefreshTokenTTL),
            CreatedByIp = ipAddress
        };
    }

    public virtual JwtSecurityToken CreateJwtSecurityToken(
        TokenOptions tokenOptions,
        User<TUserId> user,
        SigningCredentials signingCredentials,
        IList<OperationClaim<TOperationClaimId>> operationClaims,
        DateTime accessTokenExpiration
    )
    {
        return new JwtSecurityToken(
            tokenOptions.Issuer,
            tokenOptions.Audience,
            expires: accessTokenExpiration,
            notBefore: DateTime.Now,
            claims: SetClaims(user, operationClaims),
            signingCredentials: signingCredentials
        );
    }

    protected virtual IEnumerable<Claim> SetClaims(User<TUserId> user, IList<OperationClaim<TOperationClaimId>> operationClaims)
        => SetClaims(user, operationClaims, tenantId: null, isSuperAdmin: false, isImpersonating: false);

    protected virtual IEnumerable<Claim> SetClaims(
        User<TUserId> user,
        IList<OperationClaim<TOperationClaimId>> operationClaims,
        Guid? tenantId,
        bool isSuperAdmin,
        bool isImpersonating)
    {
        List<Claim> claims = [];
        claims.AddNameIdentifier(user!.Id!.ToString()!);
        claims.AddEmail(user.Email);
        claims.AddRoles(operationClaims.Select(c => c.Name).ToArray());
        claims.AddTenantId(tenantId);
        claims.AddIsSuperAdmin(isSuperAdmin);
        claims.AddIsImpersonating(isImpersonating);
        return claims.ToImmutableList();
    }

    private string randomRefreshToken()
    {
        byte[] numberByte = new byte[32];
        using var random = RandomNumberGenerator.Create();
        random.GetBytes(numberByte);
        return Convert.ToBase64String(numberByte);
    }
}
