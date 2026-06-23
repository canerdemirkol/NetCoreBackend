# Core.Security.DependencyInjection

Extension method that registers `Core.Security` services in the DI container.

## Installation

```csharp
// Program.cs
var tokenOptions = builder.Configuration
    .GetSection("TokenOptions")
    .Get<TokenOptions>()!;

builder.Services.AddSecurityServices<Guid, Guid, Guid>(tokenOptions);
```

The generic parameters are, in order: `TUserId`, `TOperationClaimId`, `TRefreshTokenId`. Change them according to the entity ID type in your application.

Registered services:

| Interface | Implementation | Lifetime |
|---|---|---|
| `ITokenHelper<TUserId, TOperationClaimId, TRefreshTokenId>` | `JwtHelper<...>` | Scoped |
| `IEmailAuthenticatorHelper` | `EmailAuthenticatorHelper` | Scoped |
| `IOtpAuthenticatorHelper` | `OtpNetOtpAuthenticatorHelper` | Scoped |
