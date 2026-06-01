# Setup Guide

NetCoreBackend (NArchitecture Core) kullanan bir ASP.NET Core projesinin sıfırdan kurulumu.
Bu doküman **consuming app** tarafıdır — framework kütüphaneleri zaten yazıldı, sen sadece
DI ve middleware bağlantılarını kuracaksın.

---

## 1. NuGet referansları

Hangisini hangi senaryoda kullanacağına bağlı, ama tipik bir multi-tenant API şu paketleri
çeker:

```xml
<ItemGroup>
  <!-- Çekirdek -->
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Application" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Persistence" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Persistence.DependencyInjection" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Persistence.WebApi" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Security" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Security.DependencyInjection" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.MultiTenancy" />

  <!-- Cross-cutting -->
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Abstraction" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Serilog" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Serilog.File" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.DependencyInjection" />

  <!-- Lokalizasyon (YAML) -->
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Localization.Abstraction" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml.DependencyInjection" />
  <PackageReference Include="NetCoreBackend.NArchitecture.Core.Localization.WebApi" />

  <!-- Üçüncü parti runtime'lar (consuming app'in seçtikleri) -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
  <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" />
  <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  <PackageReference Include="MediatR" />
</ItemGroup>
```

İhtiyacın olmayanları (örn. ElasticSearch, AmazonTranslate, MailKit) eklemeyebilirsin.

---

## 2. `appsettings.json`

```jsonc
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=MyApp;Trusted_Connection=True;TrustServerCertificate=True",
    "Redis": "localhost:6379"
  },

  "TokenOptions": {
    "Audience": "myapp-clients",
    "Issuer": "myapp.com",
    "AccessTokenExpiration": 15,           // dakika
    "SecurityKey": "min-32-byte-utf8-secret-here-please-rotate",
    "RefreshTokenTtlDays": 7               // gün — property adıyla birebir eşleşmeli
  },

  "CacheSettings": {
    "SlidingExpirationDays": 7             // ICachableRequest.SlidingExpiration override etmezse default
  },

  "FileLogConfiguration": {
    "FolderPath": "logs",                  // path traversal reddedilir (`..`, absolute path yok)
    "MinLogLevel": "Information",
    "LogOutputTemplate": "[{Timestamp:dd.MM.yyyy HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
    "SpecificLogFolders": ["UserService", "OrderService"]
  },

  "MailSettings": {
    "Server": "smtp.example.com",
    "Port": 587,
    "SenderFullName": "MyApp",
    "SenderEmail": "noreply@myapp.com",
    "UserName": "smtp-user",
    "Password": "smtp-pass",
    "AuthenticationRequired": true
  }
}
```

> JWT `SecurityKey` minimum 32 byte UTF-8 olmak zorunda; `SecurityKeyHelper` daha kısa key'i
> `ArgumentException` ile reddeder.

---

## 3. `Program.cs` — Tam Kurulum

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetCoreBackend.NArchitecture.Core.Application.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi.Extensions;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Abstraction;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Configurations;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Serilog.File;
using NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Localization.WebApi;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Persistence.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Persistence.WebApi;
using NetCoreBackend.NArchitecture.Core.Security.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Security.JWT;

var builder = WebApplication.CreateBuilder(args);

// ─── 1. Configuration objects ─────────────────────────────────────────────────
var tokenOptions = builder.Configuration
    .GetSection("TokenOptions").Get<TokenOptions>()
    ?? throw new InvalidOperationException("TokenOptions missing.");

var fileLogConfig = builder.Configuration
    .GetSection("FileLogConfiguration").Get<FileLogConfiguration>()
    ?? throw new InvalidOperationException("FileLogConfiguration missing.");

// ─── 2. EF Core DbContext ──────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ─── 3. Multi-tenancy (ITenantContext, ITenantEntitySetter, middleware) ──────
builder.Services.AddMultiTenancy();
builder.Services.AddScoped<ITenantService, MyTenantService>();   // sen yazacaksın

// ─── 4. Security (ITokenHelper, password & authenticator helpers) ────────────
builder.Services.AddSecurityServices<Guid, Guid, Guid>(tokenOptions);
//                                    ^TUserId ^TOperationClaimId ^TRefreshTokenId
//   uygulamanın entity Id tiplerine göre değiştir

// ─── 5. JWT Bearer authentication ─────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = tokenOptions.Issuer,
            ValidAudience = tokenOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SecurityKeyHelper.CreateSecurityKey(tokenOptions.SecurityKey),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// ─── 6. MediatR + tüm pipeline behavior'lar tek çağrı ────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddNArchitecturePipelineBehaviors();    // ← Authorization, Caching, Logging, ...

// ─── 7. Pipeline behavior'ların gereksinimleri ───────────────────────────────
builder.Services.AddHttpContextAccessor();                                          // Auth, Tenant, Caching, Logging
builder.Services.AddStackExchangeRedisCache(o =>                                    // CachingBehavior, CacheRemovingBehavior
    o.Configuration = builder.Configuration.GetConnectionString("Redis"));
// Alternatif (dev/local): builder.Services.AddDistributedMemoryCache();

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);               // RequestValidationBehavior

// ─── 8. Logging (ILogger from Core.CrossCuttingConcerns.Logging.Abstraction) ─
builder.Services.AddLogging(new SerilogFileLogger(fileLogConfig));                  // LoggingBehavior, ExceptionMiddleware

// ─── 9. Localization (YAML) ───────────────────────────────────────────────────
builder.Services.AddYamlResourceLocalization();    // ILocalizationService → ResourceLocalizationManager

// ─── 10. EF migration applier (startup'ta otomatik migrate eder) ─────────────
builder.Services.AddDbMigrationApplier<AppDbContext>();

// ─── 11. Controllers + Swagger ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ─── Middleware sırası KRİTİK ────────────────────────────────────────────────
app.ConfigureCustomExceptionMiddleware();   // En önde — sonraki middleware'lerden gelen exception'ları yakalar

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();

app.UseAuthentication();        // JWT parse — TenantMiddleware'den ÖNCE
app.UseMultiTenancy();          // JWT'deki tenant_id, header, subdomain
app.UseResponseLocalization();  // Accept-Language → ILocalizationService.AcceptLocales
app.UseAuthorization();

app.MapControllers();

// Migration otomatik uygula (Database.Migrate)
app.UseDbMigrationApplier();

app.Run();
```

### Middleware sırası neden bu şekilde?

```
ConfigureCustomExceptionMiddleware  ← sonra gelen her şeyin exception'ını yakalar
UseAuthentication                   ← JWT parsing burada olur, User.Claims dolar
UseMultiTenancy                     ← JWT claim'inden tenant_id okur (1. öncelik)
UseResponseLocalization             ← TenantContext.DefaultLocale fallback'i için MultiTenancy sonrası
UseAuthorization                    ← SuperAdmin / role check'leri
```

Sıra ters çevrilirse:
- `UseMultiTenancy` önce gelirse JWT henüz parse edilmediği için `User.Claims` boş, tenant
  yanlış kaynaklardan çözülür.
- `UseResponseLocalization` önce gelirse `tenantContext.DefaultLocale` henüz null, fallback
  çalışmaz.

---

## 4. DbContext — Tenant filter ve PlatformAdmin tablosu

```csharp
public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public DbSet<Product> Products => Set<Product>();
    public DbSet<User<Guid>> Users => Set<User<Guid>>();
    public DbSet<RefreshToken<Guid, Guid>> RefreshTokens => Set<RefreshToken<Guid, Guid>>();
    public DbSet<UserOperationClaim<Guid, Guid, Guid>> UserOperationClaims => Set<UserOperationClaim<Guid, Guid, Guid>>();
    public DbSet<OperationClaim<Guid>> OperationClaims => Set<OperationClaim<Guid>>();   // tenant-wide
    public DbSet<PlatformAdmin<Guid>> PlatformAdmins => Set<PlatformAdmin<Guid>>();      // ayrı tablo
    public DbSet<Tenant> Tenants => Set<Tenant>();                                       // platform-wide

    public AppDbContext(DbContextOptions<AppDbContext> opt, ITenantContext tenantContext)
        : base(opt)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Tenant.Identifier UNIQUE — TenantMiddleware GetBySlugAsync için
        builder.Entity<Tenant>().HasIndex(t => t.Identifier).IsUnique();
        builder.Entity<Tenant>()
            .HasIndex(t => t.Domain).IsUnique()
            .HasFilter("[Domain] IS NOT NULL");

        // PlatformAdmin email global unique
        builder.Entity<PlatformAdmin<Guid>>().HasIndex(a => a.Email).IsUnique();

        // Tenant user email — (TenantId, Email) composite unique
        builder.Entity<User<Guid>>().HasIndex(u => new { u.TenantId, u.Email }).IsUnique();

        // EF Core global query filter — tüm ITenantEntity'lerde
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType)) continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProp = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
            var ctxConst = Expression.Constant(_tenantContext);
            var ctxTenantId = Expression.Property(
                Expression.Property(ctxConst, nameof(ITenantContext.TenantId)),
                "Value");
            var isSuperAdmin = Expression.Property(ctxConst, nameof(ITenantContext.IsSuperAdmin));

            // SuperAdmin all rows; tenant user only their own
            var filter = Expression.OrElse(isSuperAdmin, Expression.Equal(tenantIdProp, ctxTenantId));
            builder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(filter, parameter));
        }

        base.OnModelCreating(builder);
    }
}
```

---

## 5. `ITenantService` implementasyonu

```csharp
public class MyTenantService : ITenantService
{
    private readonly AppDbContext _ctx;
    public MyTenantService(AppDbContext ctx) => _ctx = ctx;

    // IgnoreQueryFilters() — tenant lookup'ı kendi filter'ından geçmemeli
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _ctx.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => _ctx.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Identifier == slug, ct);

    public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default)
        => _ctx.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Domain == domain, ct);
}
```

---

## 6. Pipeline Behavior — Marker Interface Referansı

`AddNArchitecturePipelineBehaviors()` 9 behavior'u kaydeder; her biri opt-in.

| Behavior | Tetikleyici interface | Ne yapar |
|---|---|---|
| `AuthorizationBehavior` | `ISecuredRequest` | `Roles` array'i ile rol check'i. PlatformAdmin (`is_super_admin=true`) tüm role check'lerini atlar; `Roles=["SuperAdmin"]` istiyorsa sadece PlatformAdmin geçer. |
| `SuperAdminBlockBehavior` | `IBlockedForSuperAdmin` | PlatformAdmin'in (impersonate etmeden) tenant-user-only operasyonları çağırmasını engeller. |
| `TenantValidationBehavior` | `ITenantValidationRequest` | Aktif tenant context yoksa `AuthorizationException`. SuperAdmin muaf. |
| `RequestValidationBehavior` | otomatik (her zaman) | FluentValidation `IValidator<TRequest>` varsa çalışır, `ValidationException` → 400. |
| `CachingBehavior` | `ICachableRequest` | Distributed cache lookup → miss ise handler → cache yazımı. Key tenant-prefixed. |
| `CacheRemovingBehavior` | `ICacheRemoverRequest` | `CacheGroupKey` veya `CacheKey` ile cache invalidation. |
| `LoggingBehavior` | `ILoggableRequest` | Request body + user + tenant_id loglanır. `ISensitiveRequest` da implement edilirse payload `[redacted]`. |
| `TransactionScopeBehavior` | `ITransactionalRequest` | Handler exception fırlatırsa `TransactionScope` rollback. |
| `PerformanceBehavior` | `IIntervalRequest` | `Interval` (saniye) aşılırsa warning log. |

---

## 7. Örnek Handler — birden çok behavior aktif

```csharp
public record GetProductsQuery(int PageIndex = 0, int PageSize = 10)
    : IRequest<GetListResponse<ProductDto>>,
      ISecuredRequest,                     // AuthorizationBehavior
      ITenantValidationRequest,            // TenantValidationBehavior
      ICachableRequest,                    // CachingBehavior
      ILoggableRequest                     // LoggingBehavior
{
    public string[] Roles => ["Manager", "Admin"];
    public string CacheKey => $"Products:GetAll:{PageIndex}:{PageSize}";
    public string? CacheGroupKey => "Products";       // bir Product mutation'ında temizlenir
    public bool BypassCache => false;
    public TimeSpan? SlidingExpiration => null;       // CacheSettings default kullan
}

public class CreateProductCommand : IRequest<Guid>,
    ISecuredRequest,
    ITenantValidationRequest,
    ITransactionalRequest,
    ICacheRemoverRequest                    // CacheRemovingBehavior
{
    public required string Name { get; init; }
    public required decimal Price { get; init; }

    public string[] Roles => ["Admin"];
    public bool BypassCache => false;
    public string? CacheKey => null;        // sadece group temizliyoruz
    public string[]? CacheGroupKey => ["Products"];
}

// Payload'ı sensitive olan command
public record LoginCommand(string Email, string Password)
    : IRequest<LoginResponse>,
      ISensitiveRequest                     // LoggingBehavior body'yi "[redacted]" yapar
{ }
```

---

## 8. Cherry-pick — sadece bazı behavior'lar isteniyorsa

`AddNArchitecturePipelineBehaviors()` umbrella'sını **çağırma**, sadece istediklerini kaydet:

```csharp
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
// LoggingBehavior, TransactionScopeBehavior, vb. kaydedilmedi → bypass
```

Performans maliyeti: umbrella registration'ı kullanmanın overhead'i ihmal edilebilir
(behavior'lar opt-in çalışır, request marker implement etmiyorsa pipeline o behavior'u
çağırmaz). Cherry-pick sadece consuming app'in MediatR pipeline'ında tam kontrol istiyorsa
gerekli.

---

## 9. Migration

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

`UseDbMigrationApplier()` startup'ta bekleyen migration'ları otomatik uygular. Production'da
CI/CD pipeline'ı üzerinden ayrıca migration yapman daha güvenlidir; bu extension dev/test
için kolaylık.

---

## 10. Auth flow — kısa özet

Detaylar: [AUTH.md](./AUTH.md) ve [TENANT.md](./TENANT.md)

```
POST /api/auth/login                ← email + password (+ X-Tenant-ID for users)
   ↓
   PlatformAdmin tablosunda mı? → CreateAdminToken (is_super_admin: true)
   ↓ değil
   User tablosunda mı? (EF filter tenant'a göre)
      ↓ var → password verify → CreateToken (tenant_id claim ile)

POST /api/auth/refresh              ← refresh token + X-Tenant-ID
POST /api/auth/impersonate          ← SuperAdmin token + tenantId → impersonation token
POST /api/auth/impersonate/exit     ← impersonation/SuperAdmin token → plain SuperAdmin token
```

---

## 11. Hızlı checklist — productıon'a çıkmadan önce

- [ ] `TokenOptions.SecurityKey` ≥ 32 byte UTF-8 ve KMS/secret store'dan geliyor
- [ ] `TokenOptions.RefreshTokenTtlDays` ayarlı (0 değil — yoksa refresh anında expire)
- [ ] Redis bağlantısı çalışıyor (in-memory cache production'a uygun değil)
- [ ] `Tenant.Identifier` ve `Domain` üzerinde unique index var
- [ ] `User.Email` üzerinde `(TenantId, Email)` composite unique index var
- [ ] `OtpAuthenticator.SecretKey` için column-level encryption düşünülmüş
- [ ] `ConfigureCustomExceptionMiddleware` middleware sırasında ilk sırada
- [ ] `UseAuthentication` `UseMultiTenancy`'den önce
- [ ] Login handler `IsLegacyHash` kontrolü yapıp PBKDF2'ye lazy migration yapıyor (eski sistem migrate ediliyorsa)
- [ ] CI/CD migration ayrı bir step'te (UseDbMigrationApplier sadece dev/test için)
- [ ] Validation hataları (`PageRequest [Range]` vb.) controller'larda ModelState check'i ile yakalanıyor
- [ ] Sensitive command'lar `ISensitiveRequest` implement ediyor
