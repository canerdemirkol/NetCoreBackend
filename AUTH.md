# Authentication & Authorization Architecture

This document describes the single-endpoint authentication system, impersonation flow, and how to implement each handler in your consuming application.

---

## Overview

```
POST /api/auth/login          ← Everyone uses the same endpoint
      │
      ├─ Is this email a PlatformAdmin?  → SuperAdmin token (no tenant)
      │
      └─ Is this a User in the tenant?  → Tenant-scoped token
            (tenant resolved from X-Tenant-ID header or subdomain)
```

One login endpoint. The system identifies who is logging in by checking the email — first against `PlatformAdmins`, then against `Users`. No caller needs to know which "type" they are; the server figures it out.

Impersonation is a **separate flow** that requires an already-authenticated SuperAdmin token. It is not a login — it is "exchange my SuperAdmin token for a tenant-scoped token."

---

## Entity Model

### `User<TId>` — Tenant User

```csharp
public class User<TId> : TenantEntity<TId>  // TenantId column exists
{
    public string Email { get; set; }
    public byte[] PasswordSalt { get; set; }
    public byte[] PasswordHash { get; set; }
    public AuthenticatorType AuthenticatorType { get; set; }
}
```

- Scoped to a tenant. Same email (`john@example.com`) can exist in multiple tenants as separate accounts.
- EF Core global query filter always applies: `WHERE TenantId = @currentTenantId`.

### `PlatformAdmin<TId>` — Platform Administrator

```csharp
public class PlatformAdmin<TId> : Entity<TId>  // no TenantId column
{
    public string Email { get; set; }
    public byte[] PasswordSalt { get; set; }
    public byte[] PasswordHash { get; set; }
}
```

- Stored in a separate `PlatformAdmins` table.
- No `TenantId` — EF Core filters never apply to this table.
- Email is globally unique across the platform (typically an internal domain like `admin@platform.com`).

**Why separate tables?**
`User` is a `TenantEntity`. If SuperAdmins were stored there with `TenantId = null`, every query would need special-cased null handling and filter bypassing. A separate table keeps the model clean and eliminates an entire class of accidental data leaks.

---

## Token Structures

### Tenant User Token
```json
{
  "nameid": "user-guid",
  "email": "john@acme.com",
  "role": ["Manager"],
  "tenant_id": "acme-guid",
  "is_super_admin": "false",
  "is_impersonating": "false"
}
```

### PlatformAdmin Token
```json
{
  "nameid": "admin-guid",
  "email": "admin@platform.com",
  "role": ["SuperAdmin"],
  "is_super_admin": "true",
  "is_impersonating": "false"
}
```

### Impersonation Token
```json
{
  "nameid": "admin-guid",
  "email": "admin@platform.com",
  "role": ["SuperAdmin"],
  "tenant_id": "acme-guid",
  "is_super_admin": "true",
  "is_impersonating": "true"
}
```

---

## Flow 1 — Single Login Endpoint

```
POST /api/auth/login
X-Tenant-ID: acme                        ← required for tenant users, ignored for admins
Body: { "email": "...", "password": "..." }
```

**Resolution order:**

```
1. Check PlatformAdmins WHERE Email = @email
      ↓ found → SuperAdmin token (skip tenant check entirely)
      ↓ not found
2. Check Users WHERE Email = @email AND TenantId = @resolvedTenantId
      ↓ found → Tenant-scoped token
      ↓ not found → 401 Invalid credentials
```

**Consuming app — LoginCommand + Handler:**

```csharp
public record LoginCommand(string Email, string Password) : IRequest<LoginResponse>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IPlatformAdminRepository _adminRepo;
    private readonly IUserRepository _userRepo;
    private readonly IOperationClaimRepository _claimRepo;
    private readonly ITokenHelper<Guid, int, Guid> _tokenHelper;
    private readonly ITenantContext _tenantContext;

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        // Step 1 — check PlatformAdmins first (no tenant needed)
        PlatformAdmin<Guid>? admin = await _adminRepo.GetAsync(
            a => a.Email == request.Email, ct);

        if (admin is not null)
        {
            if (!HashingHelper.VerifyPasswordHash(request.Password, admin.PasswordHash, admin.PasswordSalt))
                throw new BusinessException("Invalid credentials.");

            var adminClaims = await _claimRepo.GetSuperAdminClaimsAsync(ct);
            AccessToken token = _tokenHelper.CreateAdminToken(admin, adminClaims);
            return new LoginResponse(token.Token, RefreshToken: null, token.ExpirationDate);
        }

        // Step 2 — check tenant Users (EF Core filter applies: WHERE TenantId = @resolvedTenantId)
        // TenantContext is already populated by TenantMiddleware from the X-Tenant-ID header/subdomain
        if (!_tenantContext.HasTenant)
            throw new BusinessException("Tenant identifier is required for user login.");

        User<Guid>? user = await _userRepo.GetAsync(u => u.Email == request.Email, ct);
        if (user is null)
            throw new BusinessException("Invalid credentials.");

        if (!HashingHelper.VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
            throw new BusinessException("Invalid credentials.");

        var userClaims = await _claimRepo.GetListByUserAsync(user.Id, ct);
        AccessToken userToken = _tokenHelper.CreateToken(user, userClaims);
        RefreshToken<Guid, Guid> refreshToken = _tokenHelper.CreateRefreshToken(user, ipAddress);
        await _refreshTokenRepo.AddAsync(refreshToken, ct);

        return new LoginResponse(userToken.Token, refreshToken.Token, userToken.ExpirationDate);
    }
}
```

**What the caller does:**

```
# Tenant user login
POST /api/auth/login
X-Tenant-ID: acme
Body: { "email": "john@acme.com", "password": "..." }
→ Token: tenant_id = acme-guid

# PlatformAdmin login (no header needed, ignored if present)
POST /api/auth/login
Body: { "email": "admin@platform.com", "password": "..." }
→ Token: is_super_admin = true
```

Same endpoint. Caller sends what they have. Server decides.

---

## Flow 2 — Impersonation

SuperAdmin wants to see exactly what a specific tenant's users see.

```
POST /api/auth/impersonate
Authorization: Bearer {superAdminToken}
Body: { "tenantId": "acme-guid" }
```

This is **not a login**. The admin is already authenticated. This is a token exchange: "give me a new token scoped to this tenant."

**Consuming app — ImpersonateCommand + Handler:**

```csharp
public record ImpersonateCommand(Guid TenantId) : IRequest<LoginResponse>, ISecuredRequest
{
    public string[] Roles => ["SuperAdmin"];
}

public class ImpersonateCommandHandler : IRequestHandler<ImpersonateCommand, LoginResponse>
{
    private readonly IPlatformAdminRepository _adminRepo;
    private readonly ITenantService _tenantService;
    private readonly ITokenHelper<Guid, int, Guid> _tokenHelper;
    private readonly IOperationClaimRepository _claimRepo;
    private readonly IHttpContextAccessor _http;

    public async Task<LoginResponse> Handle(ImpersonateCommand request, CancellationToken ct)
    {
        // Validate target tenant
        Tenant? tenant = await _tenantService.GetByIdAsync(request.TenantId, ct);
        if (tenant is null) throw new NotFoundException("Tenant not found.");
        if (!tenant.IsActive) throw new BusinessException("Tenant is not active.");

        // Identify the current admin from the token
        Guid adminId = Guid.Parse(_http.HttpContext!.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        PlatformAdmin<Guid> admin = await _adminRepo.GetAsync(a => a.Id == adminId, ct)
            ?? throw new NotFoundException("Admin not found.");

        var claims = await _claimRepo.GetSuperAdminClaimsAsync(ct);

        // Token: is_super_admin + tenant_id + is_impersonating
        AccessToken token = _tokenHelper.CreateImpersonationToken(admin, claims, request.TenantId);

        return new LoginResponse(token.Token, RefreshToken: null, token.ExpirationDate);
    }
}
```

After this, the admin uses the impersonation token. All requests are scoped to `acme-guid`. EF Core filter applies exactly as it does for a real Acme user.

---

## Flow 3 — Exit Impersonation

```
POST /api/auth/impersonate/exit
Authorization: Bearer {impersonationToken}
```

```csharp
public record ExitImpersonationCommand : IRequest<LoginResponse>, ISecuredRequest
{
    public string[] Roles => ["SuperAdmin"];
}

public class ExitImpersonationCommandHandler : IRequestHandler<ExitImpersonationCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(ExitImpersonationCommand request, CancellationToken ct)
    {
        Guid adminId = Guid.Parse(_http.HttpContext!.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        PlatformAdmin<Guid> admin = await _adminRepo.GetAsync(a => a.Id == adminId, ct)!;
        var claims = await _claimRepo.GetSuperAdminClaimsAsync(ct);

        // Back to plain SuperAdmin token — no tenant, no impersonation flag
        AccessToken token = _tokenHelper.CreateAdminToken(admin, claims);
        return new LoginResponse(token.Token, RefreshToken: null, token.ExpirationDate);
    }
}
```

---

## Using `IsImpersonating` in Business Logic

`is_impersonating` in the token does not change what data the admin sees (that is controlled by `TenantId`). It marks **how** the admin got that token, so the consuming app can apply additional constraints:

```csharp
// Option A — inline check in a handler
if (_tenantContext.IsImpersonating)
    throw new AuthorizationException("Write operations are disabled in impersonation mode.");

// Option B — dedicated pipeline behavior
public class ImpersonationGuardBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IBlockedDuringImpersonation
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (_tenantContext.IsImpersonating)
            throw new AuthorizationException("This operation is blocked during impersonation.");
        return await next();
    }
}

// Mark commands that must be blocked
public class CreatePaymentCommand : IRequest<PaymentResponse>,
    ISecuredRequest,
    IBlockedDuringImpersonation   ← impersonating admin cannot submit payments
{
    public string[] Roles => ["Admin"];
}

// Audit logging
if (_tenantContext.IsImpersonating)
    _auditLog.Write($"[IMPERSONATION] {adminEmail} accessed {resource} in tenant {_tenantContext.TenantId}");
```

---

## Complete Flow Diagram

```
POST /api/auth/login
{ email, password }
[X-Tenant-ID: acme]          optional
        │
        ▼
  PlatformAdmin?
  WHERE Email = @email
        │
    ┌───┴───┐
   YES      NO
    │        │
    │    HasTenant?  ← TenantContext from header/subdomain
    │        │
    │    ┌───┴───┐
    │   YES      NO → 400 "Tenant required"
    │    │
    │   User?
    │   WHERE Email = @email
    │   AND TenantId = @tenantId   ← EF Core auto-filter
    │        │
    │    ┌───┴───┐
    │   YES      NO → 401 "Invalid credentials"
    │    │
    │   Verify password
    │    │
    ├────┘
    │
    ├─ PlatformAdmin → CreateAdminToken()
    │                  { is_super_admin: true }
    │
    └─ User → CreateToken()
              { tenant_id: "acme-guid" }
```

```
POST /api/auth/impersonate        ← requires SuperAdmin token
{ tenantId: "acme-guid" }
        │
        ▼
  CreateImpersonationToken()
  { is_super_admin: true, tenant_id: "acme-guid", is_impersonating: true }

POST /api/auth/impersonate/exit   ← requires any SuperAdmin token
        │
        ▼
  CreateAdminToken()
  { is_super_admin: true }        ← back to platform-wide access
```

---

## IPlatformAdminRepository

```csharp
public interface IPlatformAdminRepository : IAsyncRepository<PlatformAdmin<Guid>, Guid> { }

public class PlatformAdminRepository
    : EfRepositoryBase<PlatformAdmin<Guid>, Guid, AppDbContext>, IPlatformAdminRepository
{
    // No ITenantEntitySetter — PlatformAdmin is not a tenant entity
    public PlatformAdminRepository(AppDbContext context) : base(context) { }
}
```

---

## Database Schema

```sql
-- Tenant users — TenantId column present
CREATE TABLE Users (
    Id           UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId     UNIQUEIDENTIFIER NOT NULL,
    Email        NVARCHAR(256) NOT NULL,
    PasswordHash VARBINARY(MAX) NOT NULL,
    PasswordSalt VARBINARY(MAX) NOT NULL,
    CreatedDate  DATETIME2 NOT NULL,
    DeletedDate  DATETIME2 NULL
);
CREATE INDEX IX_Users_Tenant_Email ON Users (TenantId, Email);

-- Platform admins — no TenantId column
CREATE TABLE PlatformAdmins (
    Id           UNIQUEIDENTIFIER PRIMARY KEY,
    Email        NVARCHAR(256) NOT NULL UNIQUE,
    PasswordHash VARBINARY(MAX) NOT NULL,
    PasswordSalt VARBINARY(MAX) NOT NULL,
    CreatedDate  DATETIME2 NOT NULL
);
```

---

## Endpoint Summary

| Endpoint | Who | Requires | Returns |
|---|---|---|---|
| `POST /api/auth/login` | Everyone | email + password (+ X-Tenant-ID for users) | Token matching the caller's identity |
| `POST /api/auth/impersonate` | PlatformAdmin | SuperAdmin token + tenantId | Tenant-scoped impersonation token |
| `POST /api/auth/impersonate/exit` | PlatformAdmin | SuperAdmin or impersonation token | Plain SuperAdmin token |

---

## Middleware Pipeline

```
UseRouting()
UseAuthentication()            ← JWT parsed
UseMultiTenancy()              ← JWT claim → header → subdomain → ITenantContext set
UseLocalizationMiddleware()    ← Accept-Language → ITenantContext.DefaultLocale fallback
UseAuthorization()             ← role checks; SuperAdmin bypasses via IsSuperAdmin()
MapControllers()
```
