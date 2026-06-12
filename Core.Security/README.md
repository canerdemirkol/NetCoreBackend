# Core.Security

JWT tabanlı kimlik doğrulama, şifreleme, OTP/Email authenticator ve claim yönetimi altyapısı.

## Entity'ler

Entity hiyerarşisi tenant izolasyonunu yansıtır:

| Entity | Taban | Açıklama |
|---|---|---|
| `User<TId>` | `TenantEntity` | Email, şifre hash/salt, authenticator tipi. Aynı email farklı tenant'larda var olabilir. |
| `RefreshToken<TId, TUserId>` | `TenantEntity` | Tenant bazlı token yönetimi. Tenant silinince tüm token'lar tek sorguda iptal edilir. |
| `UserOperationClaim<TId, TUserId, TOperationClaimId>` | `TenantEntity` | Tenant bazlı kullanıcı–izin eşleşmesi. |
| `EmailAuthenticator<TId>` | `TenantEntity` | Email doğrulama kodu. `TId` PK tipi (User'ın ID tipiyle aynı olması beklenir). |
| `OtpAuthenticator<TId>` | `TenantEntity` | TOTP tabanlı 2FA. `SecretKey` ham byte[] — production'da `AesGcmEncryptionHelper` ile encrypt'lenip öyle saklanmalı (aşağı bkz.). |
| `OperationClaim<TId>` | `Entity` | İzin / rol kaydı. Platform genelinde ortaktır, tenant'a özgü değildir. |
| `AdminRefreshToken<TId, TAdminId>` | `Entity` | PlatformAdmin refresh token'ı. Tenant dışı olduğundan `TenantEntity` değil, `Entity`'den türer. |

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

// Normal kullanıcı token'ı (tenant_id claim otomatik user.TenantId'den alınır)
AccessToken token = jwtHelper.CreateToken(user, operationClaims);

// PlatformAdmin token'ı (is_super_admin: true, tenant_id yok)
AccessToken token = jwtHelper.CreateAdminToken(admin, operationClaims);

// PlatformAdmin refresh token'ı
AdminRefreshToken<TRefreshTokenId, TUserId> rt = jwtHelper.CreateAdminRefreshToken(admin, ipAddress);

// PlatformAdmin impersonation (is_super_admin: true + tenant_id + is_impersonating: true)
AccessToken token = jwtHelper.CreateImpersonationToken(admin, operationClaims, tenantId);
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

PBKDF2-HMAC-SHA512, 210.000 iterasyon (OWASP 2024 minimum). Eski HMACSHA512 hash'leri
salt boyutundan otomatik tespit edilip backward-compat verify ile çalışmaya devam eder.

```csharp
// Şifre hash'leme (yeni format)
HashingHelper.CreatePasswordHash("password", out byte[] hash, out byte[] salt);

// Doğrulama (PBKDF2 + legacy HMACSHA512 otomatik destekli)
bool ok = HashingHelper.VerifyPasswordHash("password", hash, salt);

// Login handler'ında lazy migration:
if (ok && HashingHelper.IsLegacyHash(user.PasswordSalt))
{
    HashingHelper.CreatePasswordHash(plainPassword, out var newHash, out var newSalt);
    user.PasswordHash = newHash;
    user.PasswordSalt = newSalt;
    await userRepo.UpdateAsync(user);
}
```

## Authenticator'lar

```csharp
// Email doğrulama kodu üretme (IEmailAuthenticatorHelper inject edilir)
string activationKey = await emailAuthenticatorHelper.CreateEmailActivationKey();
string code = await emailAuthenticatorHelper.CreateEmailActivationCode();

// OTP (Google Authenticator uyumlu TOTP — IOtpAuthenticatorHelper inject edilir)
byte[] secretKey = await otpAuthenticatorHelper.GenerateSecretKey();
bool valid = await otpAuthenticatorHelper.VerifyCode(secretKey, userEnteredCode);
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
```

## Sabitler

```csharp
TenantClaimTypes.TenantId        // "tenant_id"
TenantClaimTypes.IsSuperAdmin    // "is_super_admin"
TenantClaimTypes.IsImpersonating // "is_impersonating"

GeneralOperationClaims.Admin     // "Admin"
```

## PlatformAdmin

Tenant dışı platform yöneticisi. `Entity<TId>`'den türer — `TenantId` sütunu yoktur, EF Core query filter hiç uygulanmaz.

```csharp
public class PlatformAdmin<TId> : Entity<TId>
{
    public string Email { get; set; }
    public byte[] PasswordSalt { get; set; }
    public byte[] PasswordHash { get; set; }
}
```

Normal `User<TId>` ile aynı tabloda değildir. Consuming app'te ayrı bir `PlatformAdmins` tablosuna map edilir.

## JWT — Admin ve Impersonation Token'ları

```csharp
ITokenHelper<TUserId, TOperationClaimId, TRefreshTokenId>

// Tenant kullanıcı (var olan)
AccessToken token = tokenHelper.CreateToken(user, claims);

// PlatformAdmin — is_super_admin: true, tenant_id yok
AccessToken token = tokenHelper.CreateAdminToken(admin, claims);

// PlatformAdmin refresh token
AdminRefreshToken<TRefreshTokenId, TUserId> rt = tokenHelper.CreateAdminRefreshToken(admin, ipAddress);

// Impersonation — is_super_admin: true + tenant_id + is_impersonating: true
AccessToken token = tokenHelper.CreateImpersonationToken(admin, claims, tenantId);
```

Detaylı akış ve consuming app implementasyonu: [AUTH.md](../AUTH.md)

## AuthenticatorType Enum

```
None   → Sadece şifre
Email  → Email OTP
Otp    → Google Authenticator / TOTP
Sms    → SMS (genişletilebilir)
```

## At-Rest Encryption — `AesGcmEncryptionHelper`

Hash'lenemeyen ama saklanması/geri okunması gereken sensitive payload'lar için. Tipik kullanım: TOTP secret key, OAuth refresh token'ları, 3rd-party API key'leri, recovery code'lar.

**Algoritma:** AES-256-GCM (authenticated encryption). Blob layout: `[12-byte nonce][16-byte tag][ciphertext]`. Byte oynatılmış / yanlış key ile decrypt edilen blob `CryptographicException` ile reddedilir — exception mesajı blob length + associatedData presence info'su içerir (key rotation debug'ı kolaylaşır).

### Kurulum

```csharp
// Program.cs — master key SECRET STORE'dan gelmeli (KeyVault, AWS Secrets Manager, vb.)
byte[] masterKey = Convert.FromBase64String(
    builder.Configuration["EncryptionMasterKey"]
    ?? throw new InvalidOperationException("EncryptionMasterKey eksik."));
builder.Services.AddSingleton(new EncryptionMasterKey(masterKey));
```

`EncryptionMasterKey(byte[])` ctor 32-byte uzunluk doğrulamasını yapar; daha kısa key `ArgumentException` ile reddedilir. Ctor defensive copy alır, `Value` getter da her okumada copy döndürür → caller'ın master key buffer'ını mutasyonla bozması mümkün değil. Allocation-free hot path için `key.AsSpan()` kullan.

### TOTP secret encryption örneği

```csharp
public sealed class OtpEnrollmentHandler
{
    private readonly EncryptionMasterKey _key;
    private readonly IOtpAuthenticatorHelper _otp;
    private readonly IRepository<OtpAuthenticator<Guid>, Guid> _repo;

    public async Task EnrollAsync(Guid userId, CancellationToken ct)
    {
        byte[] plainSecret = await _otp.GenerateSecretKey();

        // associatedData ile user binding: bir DB row'unu başka user'a kopyalamak
        // decrypt'i fail ettirir.
        byte[] encryptedSecret = AesGcmEncryptionHelper.Encrypt(
            plaintext: plainSecret,
            key: _key.Value,
            associatedData: Encoding.UTF8.GetBytes($"otp:{userId}"));

        await _repo.AddAsync(new OtpAuthenticator<Guid>
        {
            UserId    = userId,
            SecretKey = encryptedSecret    // DB'de ŞİFRELİ durur
        });
    }

    public async Task<bool> VerifyAsync(Guid userId, string submittedCode)
    {
        OtpAuthenticator<Guid>? stored = await _repo.GetAsync(o => o.UserId == userId);
        if (stored is null) return false;

        byte[] plainSecret = AesGcmEncryptionHelper.Decrypt(
            blob: stored.SecretKey,
            key: _key.Value,
            associatedData: Encoding.UTF8.GetBytes($"otp:{userId}"));

        return await _otp.VerifyCode(plainSecret, submittedCode);
    }
}
```

### Master key rotation

Blob'a kendi versiyon byte'ını prefix'le (`[0x01][nonce][tag][ciphertext]`), decrypt sırasında prefix'e göre key seç. Migration: eski key ile decrypt → yeni key ile re-encrypt, batched job.

> Production setup (secret manager seçenekleri + appsettings örneği): [SETUP.md § 12](../SETUP.md#12-outbox--rabbitmq-configuration).

---

## RefreshToken — Rotation & Theft Detection

`RefreshToken<TId, TUserId>` artık computed flag'lerle gelir:

```csharp
token.IsExpired   // DateTime.UtcNow >= ExpirationDate
token.IsRevoked   // RevokedDate.HasValue
token.IsActive    // !IsRevoked && !IsExpired
```

`ExpirationDate` her zaman **UTC** olarak yorumlanır.

### Rotation pattern

Her refresh handle'ında eski token revoke edilir + yeni token verilir; ikisi `ReplacedByToken` ile zincirlenir.

```csharp
public sealed class RefreshAccessTokenHandler
{
    public async Task<AccessToken> Handle(RefreshCommand cmd, CancellationToken ct)
    {
        RefreshToken<Guid, Guid>? presented =
            await _tokenRepo.GetAsync(t => t.Token == cmd.RefreshToken);

        if (presented is null)
            throw new AuthorizationException("Unknown refresh token.");

        // REUSE DETECTION — presented token zaten revoke edilmişse, biri replay yapıyor.
        // Tüm family'yi (aynı user'ın aktif refresh token'larını) iptal et.
        if (presented.IsRevoked)
        {
            IList<RefreshToken<Guid, Guid>> family =
                await _tokenRepo.GetListAsync(t => t.UserId == presented.UserId);

            RefreshTokenRotation.DetectReuseAndRevokeFamily(presented, family, cmd.CallerIp);
            await _tokenRepo.UpdateRangeAsync(family);

            throw new AuthorizationException("Refresh token reuse detected — re-login required.");
        }

        if (!presented.IsActive)
            throw new AuthorizationException("Refresh token expired.");

        // Normal rotation
        AccessToken accessToken = _jwt.CreateToken(user, claims);
        RefreshToken<Guid, Guid> replacement = _jwt.CreateRefreshToken(user, cmd.CallerIp);

        RefreshTokenRotation.Rotate(presented, replacement, cmd.CallerIp);
        await _tokenRepo.UpdateAsync(presented);
        await _tokenRepo.AddAsync(replacement);

        return accessToken;
    }
}
```

**Why this matters:** çalınan refresh token'la attacker rotate ederse — legitimate user bir sonraki refresh'inde "already revoked" durumuyla karşılaşır → family revoke tetiklenir → her iki taraf da re-login'e zorlanır. Token sızıntısı kalıcı erişime dönüşmez.

---

## AdminRefreshToken — PlatformAdmin Refresh Token'ı

`AdminRefreshToken<TId, TAdminId>` PlatformAdmin'e özgü refresh token entity'sidir. `TenantEntity` değil `Entity`'den türer — tenant scope'u yoktur, EF Core global query filter uygulanmaz.

```csharp
token.IsExpired   // DateTime.UtcNow >= ExpirationDate
token.IsRevoked   // RevokedDate.HasValue
token.IsActive    // !IsRevoked && !IsExpired
```

`ITokenHelper` üzerinden üretilir:

```csharp
AdminRefreshToken<TRefreshTokenId, TUserId> rt =
    tokenHelper.CreateAdminRefreshToken(admin, ipAddress);
```

> **Breaking change (v3.0.0):** `ITokenHelper` arayüzüne `CreateAdminRefreshToken` eklendi. `ITokenHelper`'ı doğrudan implement eden sınıfların bu metodu eklemesi gerekir.

Rotation ve reuse detection mantığı tenant `RefreshToken` ile aynıdır; consuming app'te `AdminId` üzerinden family revoke uygulanır.
