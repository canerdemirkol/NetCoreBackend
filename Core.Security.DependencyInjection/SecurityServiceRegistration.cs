using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Security.EmailAuthenticator;
using NetCoreBackend.NArchitecture.Core.Security.JWT;
using NetCoreBackend.NArchitecture.Core.Security.OtpAuthenticator;
using NetCoreBackend.NArchitecture.Core.Security.OtpAuthenticator.OtpNet;

namespace NetCoreBackend.NArchitecture.Core.Security.DependencyInjection;

public static class SecurityServiceRegistration
{
    public static IServiceCollection AddSecurityServices<TUserId, TOperationClaimId, TRefreshTokenId>(
        this IServiceCollection services,
        TokenOptions tokenOptions
    )
    {
        services.AddScoped<
            ITokenHelper<TUserId, TOperationClaimId, TRefreshTokenId>,
            JwtHelper<TUserId, TOperationClaimId, TRefreshTokenId>
        >(_ => new JwtHelper<TUserId, TOperationClaimId, TRefreshTokenId>(tokenOptions));
        services.AddScoped<IEmailAuthenticatorHelper, EmailAuthenticatorHelper>();
        services.AddScoped<IOtpAuthenticatorHelper, OtpNetOtpAuthenticatorHelper>();

        return services;
    }
}
