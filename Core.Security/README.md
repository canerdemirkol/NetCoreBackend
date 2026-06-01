# Core.Security

JWT tabanlı kimlik doğrulama, şifreleme, OTP/Email authenticator ve claim yönetimi altyapısı.

## Entity'ler

Entity hiyerarşisi tenant izolasyonunu yansıtır:

| Entity | Taban | Açıklama |
|---|---|---|
| `User<TId>` | `TenantEntity` | Email, şifre hash/salt, authenticator tipi. Aynı email farklı tenant'larda var olabilir. |
| `RefreshToken<TId, TUserId>` | `TenantEntity` | Tenant bazlı token yönetimi. Tenant silinince tüm token'lar tek sorguda iptal edilir. |
| `UserOperationClaim<TId, TUserId, TOperationClaimId>` | `TenantEntity` | Tenant bazlı kullanıcı–izin eşleşmesi. |
| `EmailAuthenticator<TUserId>` | `TenantEntity` | Email doğrulama kodu. |
| `OtpAuthenticator<TUserId>` | `TenantEntity` | TOTP tabanlı 2FA. |
| `OperationClaim<TId>` | `Entity` | İzin / rol kaydı. Platform genelinde ortaktır, tenant'a özgü değildir. |

`TenantEntity` olan entity'lerin tablosunda `TenantId` sütunu fiziksel olarak bulunur. EF Core global query filter tüm SELECT sorgularına otomatik `WHERE TenantId = @currentTenantId` ekler.

## Login Flow (Tenant-Aware User)

Kullanıcının henüz JWT'si olmadığından login endpoint'inde tenant JWT claim'den değil, header veya subdomain'den çözümlenir:

```
POST /api/auth/login
X-Tenant-ID: acme          ← TenantMiddleware bunu okur
Body: { email, password }
```

```csharp
// Handler içinde ekstra bir şey yapmana gerek yok.
// TenantMiddleware X-Tenant-ID'yi okuyup TenantContext'i set etmiş olur.
// EF Core global filter login sorgusuna da otomatik uygulanır:
// SELECT * FROM Users WHERE Email = @email AND TenantId = 'acme-guid'
var user = await _userRepository.GetAsync(u => u.Email == request.Email);
```

## JWT

```csharp
JwtHelper<TUserId, TOperationClaimId, TRefreshTokenId>

// Normal kullanıcı token'ı
AccessToken token = jwtHelper.CreateToken(user, operationClaims, tenantId: tenant.Id);

// SuperAdmin token'ı (tenant yok)
AccessToken token = jwtHelper.CreateToken(user, operationClaims, tenantId: null, isSuperAdmin: true);

// SuperAdmin impersonation
AccessToken token = jwtHelper.CreateToken(user, operationClaims, tenantId: targetId, isSuperAdmin: true, isImpersonating: true);
```

`TokenOptions` (appsettings.json):
```json
{
  "TokenOptions": {
    "Audience": "your-audience",
    "Issuer": "your-issuer",
    "SecurityKey": "your-secret-key-min-32-chars",
    "AccessTokenExpiration": 15,
    "RefreshTokenTtlDays": 7
  }
}
```

## Hashing

```csharp
// Şifre hash'leme
HashingHelper.CreatePasswordHash("password", out byte[] hash, out byte[] salt);

// Doğrulama
bool ok = HashingHelper.VerifyPasswordHash("password", hash, salt);
```

## Authenticator'lar

```csharp
// Email doğrulama kodu üretme
string activationKey = EmailAuthenticatorHelper.GenerateEmailActivationKey();
string code = await EmailAuthenticatorHelper.GenerateEmailActivationCodeAsync(key);

// OTP (Google Authenticator uyumlu TOTP)
string secret = OtpAuthenticatorHelper.GenerateSecretKey();
bool valid = OtpAuthenticatorHelper.ValidateCode(secret, userEnteredCode);
```

## Claim Extension'ları

```csharp
// Claim ekleme (ICollection<Claim>)
claims.AddEmail("user@example.com");
claims.AddNameIdentifier(userId);
claims.AddRoles(["Admin", "Manager"]);
claims.AddTenantId(tenantId);
claims.AddIsSuperAdmin(true);
claims.AddIsImpersonating(false);

// Claim okuma (ClaimsPrincipal)
Guid? tenantId = user.GetTenantId();
bool isSuperAdmin = user.IsSuperAdmin();
bool isImpersonating = user.IsImpersonating();
string? email = user.GetEmail();
```

## Sabitler

```csharp
TenantClaimTypes.TenantId        // "tenant_id"
TenantClaimTypes.IsSuperAdmin    // "is_super_admin"
TenantClaimTypes.IsImpersonating // "is_impersonating"

GeneralOperationClaims.Admin     // "Admin"
```

## AuthenticatorType Enum

```
None   → Sadece şifre
Email  → Email OTP
Otp    → Google Authenticator / TOTP
Sms    → SMS (genişletilebilir)
```
