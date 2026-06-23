# Core.Security

Infrastructure for JWT-based authentication, password hashing, OTP/Email authenticators, and claim management.

## Entities

The entity hierarchy reflects tenant isolation:

| Entity | Base | Description |
|---|---|---|
| `User<TId>` | `TenantEntity` | Email, password hash/salt, authenticator type. The same email can exist across different tenants. |
| `RefreshToken<TId, TUserId>` | `TenantEntity` | Tenant-scoped token management. When a tenant is deleted, all of its tokens are revoked in a single query. |
| `UserOperationClaim<TId, TUserId, TOperationClaimId>` | `TenantEntity` | Tenant-scoped user-to-permission mapping. |
| `EmailAuthenticator<TId>` | `TenantEntity` | Email verification code. `TId` is the PK type (expected to match the User's ID type). |
| `OtpAuthenticator<TId>` | `TenantEntity` | TOTP-based 2FA. `SecretKey` is a raw byte[] — in production it must be encrypted with `AesGcmEncryptionHelper` before being stored (see below). |
| `OperationClaim<TId>` | `Entity` | Permission / role record. Shared platform-wide, not tenant-specific. |
| `AdminRefreshToken<TId, TAdminId>` | `Entity` | PlatformAdmin refresh token. Because it is outside the tenant scope, it derives from `Entity`, not `TenantEntity`. |

Tables for entities that are `TenantEntity` physically include a `TenantId` column. The EF Core global query filter automatically appends `WHERE TenantId = @currentTenantId` to every SELECT query.

## Login Flow (Tenant-Aware User)

Since the user does not yet have a JWT, the tenant on the login endpoint is resolved not from a JWT claim but from a header or subdomain:

```
POST /api/auth/login
X-Tenant-ID: acme          ← TenantMiddleware bunu okur
Body: { email, password }
```

```csharp
// You don't need to do anything extra inside the handler.
// TenantMiddleware will have read X-Tenant-ID and set the TenantContext.
// The EF Core global filter is also applied automatically to the login query:
// SELECT * FROM Users WHERE Email = @email AND TenantId = 'acme-guid'
var user = await _userRepository.GetAsync(u => u.Email == request.Email);
```

## JWT

```csharp
JwtHelper<TUserId, TOperationClaimId, TRefreshTokenId>

// Regular user token (the tenant_id claim is taken automatically from user.TenantId)
AccessToken token = jwtHelper.CreateToken(user, operationClaims);

// PlatformAdmin token (is_super_admin: true, no tenant_id)
AccessToken token = jwtHelper.CreateAdminToken(admin, operationClaims);

// PlatformAdmin refresh token
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

PBKDF2-HMAC-SHA512, 210,000 iterations (OWASP 2024 minimum). Legacy HMACSHA512 hashes
are automatically detected from the salt size and continue to work via backward-compatible verification.

```csharp
// Password hashing (new format)
HashingHelper.CreatePasswordHash("password", out byte[] hash, out byte[] salt);

// Verification (PBKDF2 + legacy HMACSHA512 supported automatically)
bool ok = HashingHelper.VerifyPasswordHash("password", hash, salt);

// Lazy migration in the login handler:
if (ok && HashingHelper.IsLegacyHash(user.PasswordSalt))
{
    HashingHelper.CreatePasswordHash(plainPassword, out var newHash, out var newSalt);
    user.PasswordHash = newHash;
    user.PasswordSalt = newSalt;
    await userRepo.UpdateAsync(user);
}
```

## Authenticators

```csharp
// Generate an email verification code (IEmailAuthenticatorHelper is injected)
string activationKey = await emailAuthenticatorHelper.CreateEmailActivationKey();
string code = await emailAuthenticatorHelper.CreateEmailActivationCode();

// OTP (Google Authenticator-compatible TOTP — IOtpAuthenticatorHelper is injected)
byte[] secretKey = await otpAuthenticatorHelper.GenerateSecretKey();
bool valid = await otpAuthenticatorHelper.VerifyCode(secretKey, userEnteredCode);
```

## Claim Extensions

```csharp
// Adding claims (ICollection<Claim>)
claims.AddEmail("user@example.com");
claims.AddNameIdentifier(userId);
claims.AddRoles(["Admin", "Manager"]);
claims.AddTenantId(tenantId);
claims.AddIsSuperAdmin(true);
claims.AddIsImpersonating(false);

// Reading claims (ClaimsPrincipal)
Guid? tenantId = user.GetTenantId();
bool isSuperAdmin = user.IsSuperAdmin();
bool isImpersonating = user.IsImpersonating();
```

## Constants

```csharp
TenantClaimTypes.TenantId        // "tenant_id"
TenantClaimTypes.IsSuperAdmin    // "is_super_admin"
TenantClaimTypes.IsImpersonating // "is_impersonating"

GeneralOperationClaims.Admin     // "Admin"
```

## PlatformAdmin

A platform administrator outside the tenant scope. Derives from `Entity<TId>` — it has no `TenantId` column, and the EF Core query filter is never applied.

```csharp
public class PlatformAdmin<TId> : Entity<TId>
{
    public string Email { get; set; }
    public byte[] PasswordSalt { get; set; }
    public byte[] PasswordHash { get; set; }
}
```

It is not in the same table as a regular `User<TId>`. In the consuming app, it is mapped to a separate `PlatformAdmins` table.

## JWT — Admin and Impersonation Tokens

```csharp
ITokenHelper<TUserId, TOperationClaimId, TRefreshTokenId>

// Tenant user (existing)
AccessToken token = tokenHelper.CreateToken(user, claims);

// PlatformAdmin — is_super_admin: true, no tenant_id
AccessToken token = tokenHelper.CreateAdminToken(admin, claims);

// PlatformAdmin refresh token
AdminRefreshToken<TRefreshTokenId, TUserId> rt = tokenHelper.CreateAdminRefreshToken(admin, ipAddress);

// Impersonation — is_super_admin: true + tenant_id + is_impersonating: true
AccessToken token = tokenHelper.CreateImpersonationToken(admin, claims, tenantId);
```

Detailed flow and consuming-app implementation: [AUTH.md](../AUTH.md)

## AuthenticatorType Enum

```
None   → Password only
Email  → Email OTP
Otp    → Google Authenticator / TOTP
Sms    → SMS (extensible)
```

## At-Rest Encryption — `AesGcmEncryptionHelper`

For sensitive payloads that cannot be hashed but must be stored and read back. Typical use: TOTP secret keys, OAuth refresh tokens, 3rd-party API keys, recovery codes.

**Algorithm:** AES-256-GCM (authenticated encryption). Blob layout: `[12-byte nonce][16-byte tag][ciphertext]`. A blob that has been tampered with or decrypted with the wrong key is rejected with a `CryptographicException` — the exception message includes the blob length and associatedData presence info (making key rotation debugging easier).

### Setup

```csharp
// Program.cs — the master key must come from a SECRET STORE (KeyVault, AWS Secrets Manager, etc.)
byte[] masterKey = Convert.FromBase64String(
    builder.Configuration["EncryptionMasterKey"]
    ?? throw new InvalidOperationException("EncryptionMasterKey is missing."));
builder.Services.AddSingleton(new EncryptionMasterKey(masterKey));
```

The `EncryptionMasterKey(byte[])` ctor validates the 32-byte length; a shorter key is rejected with an `ArgumentException`. The ctor takes a defensive copy, and the `Value` getter also returns a copy on every read → it is impossible for the caller to corrupt the master key buffer through mutation. For an allocation-free hot path, use `key.AsSpan()`.

### TOTP secret encryption example

```csharp
public sealed class OtpEnrollmentHandler
{
    private readonly EncryptionMasterKey _key;
    private readonly IOtpAuthenticatorHelper _otp;
    private readonly IRepository<OtpAuthenticator<Guid>, Guid> _repo;

    public async Task EnrollAsync(Guid userId, CancellationToken ct)
    {
        byte[] plainSecret = await _otp.GenerateSecretKey();

        // User binding via associatedData: copying a DB row to another user
        // causes decryption to fail.
        byte[] encryptedSecret = AesGcmEncryptionHelper.Encrypt(
            plaintext: plainSecret,
            key: _key.Value,
            associatedData: Encoding.UTF8.GetBytes($"otp:{userId}"));

        await _repo.AddAsync(new OtpAuthenticator<Guid>
        {
            UserId    = userId,
            SecretKey = encryptedSecret    // stored ENCRYPTED in the DB
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

Prefix the blob with your own version byte (`[0x01][nonce][tag][ciphertext]`), and select the key based on the prefix during decryption. Migration: decrypt with the old key → re-encrypt with the new key, as a batched job.

> Production setup (secret manager options + appsettings example): [SETUP.md § 12](../SETUP.md#12-outbox--rabbitmq-configuration).

---

## RefreshToken — Rotation & Theft Detection

`RefreshToken<TId, TUserId>` now comes with computed flags:

```csharp
token.IsExpired   // DateTime.UtcNow >= ExpirationDate
token.IsRevoked   // RevokedDate.HasValue
token.IsActive    // !IsRevoked && !IsExpired
```

`ExpirationDate` is always interpreted as **UTC**.

### Rotation pattern

On each refresh handle, the old token is revoked and a new token is issued; the two are chained via `ReplacedByToken`.

```csharp
public sealed class RefreshAccessTokenHandler
{
    public async Task<AccessToken> Handle(RefreshCommand cmd, CancellationToken ct)
    {
        RefreshToken<Guid, Guid>? presented =
            await _tokenRepo.GetAsync(t => t.Token == cmd.RefreshToken);

        if (presented is null)
            throw new AuthorizationException("Unknown refresh token.");

        // REUSE DETECTION — if the presented token is already revoked, someone is replaying it.
        // Revoke the entire family (the same user's active refresh tokens).
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

**Why this matters:** if an attacker rotates with a stolen refresh token — the legitimate user encounters the "already revoked" state on their next refresh → a family revoke is triggered → both parties are forced to re-login. A token leak does not turn into permanent access.

---

## AdminRefreshToken — PlatformAdmin Refresh Token

`AdminRefreshToken<TId, TAdminId>` is the refresh token entity specific to PlatformAdmin. It derives from `Entity`, not `TenantEntity` — it has no tenant scope, and the EF Core global query filter is not applied.

```csharp
token.IsExpired   // DateTime.UtcNow >= ExpirationDate
token.IsRevoked   // RevokedDate.HasValue
token.IsActive    // !IsRevoked && !IsExpired
```

It is produced via `ITokenHelper`:

```csharp
AdminRefreshToken<TRefreshTokenId, TUserId> rt =
    tokenHelper.CreateAdminRefreshToken(admin, ipAddress);
```

> **Breaking change (v3.0.0):** `CreateAdminRefreshToken` was added to the `ITokenHelper` interface. Classes that implement `ITokenHelper` directly must add this method.

The rotation and reuse-detection logic is the same as for the tenant `RefreshToken`; in the consuming app, the family revoke is applied via `AdminId`.
