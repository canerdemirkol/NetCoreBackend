# Core.Security.DependencyInjection

`Core.Security` servislerini DI container'a kaydeden extension metot.

## Kurulum

```csharp
// Program.cs
var tokenOptions = builder.Configuration
    .GetSection("TokenOptions")
    .Get<TokenOptions>()!;

builder.Services.AddSecurityServices<Guid, Guid, Guid>(tokenOptions);
```

Generic parametreler sırasıyla: `TUserId`, `TOperationClaimId`, `TRefreshTokenId`. Uygulamanızdaki entity ID tipine göre değiştirin.

Kaydedilen servisler:

| Arayüz | İmplementasyon | Yaşam Döngüsü |
|---|---|---|
| `ITokenHelper<TUserId, TOperationClaimId, TRefreshTokenId>` | `JwtHelper<...>` | Scoped |
| `IEmailAuthenticatorHelper` | `EmailAuthenticatorHelper` | Scoped |
| `IOtpAuthenticatorHelper` | `OtpNetOtpAuthenticatorHelper` | Scoped |
