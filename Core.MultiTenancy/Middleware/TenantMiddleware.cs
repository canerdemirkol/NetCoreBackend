using Microsoft.AspNetCore.Http;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Constants;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Context;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Entities;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Exceptions;

namespace NetCoreBackend.NArchitecture.Core.MultiTenancy.Middleware;

public class TenantMiddleware
{
    private const string TenantHeader = "X-Tenant-ID";
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantService tenantService, TenantContext tenantContext)
    {
        bool isSuperAdmin = context.User.FindFirst(TenantClaimTypes.IsSuperAdmin)?.Value == "true";
        bool isImpersonating = context.User.FindFirst(TenantClaimTypes.IsImpersonating)?.Value == "true";
        string? tenantIdClaim = context.User.FindFirst(TenantClaimTypes.TenantId)?.Value;

        // SuperAdmin without impersonation → bypass tenant filter
        if (isSuperAdmin && tenantIdClaim == null)
        {
            tenantContext.SetSuperAdmin();
            await _next(context);
            return;
        }

        Tenant? tenant = await ResolveTenantAsync(context, tenantService, tenantIdClaim);

        if (tenant == null)
        {
            await _next(context);
            return;
        }

        if (!tenant.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync($"Tenant '{tenant.Identifier}' is not active.");
            return;
        }

        if (isSuperAdmin)
            tenantContext.SetSuperAdmin();

        if (isImpersonating)
            tenantContext.SetImpersonating();

        tenantContext.SetTenant(tenant.Id, tenant.Identifier, tenant.DefaultLocale);
        await _next(context);
    }

    private static async Task<Tenant?> ResolveTenantAsync(HttpContext context, ITenantService tenantService, string? tenantIdClaim)
    {
        // Priority 1: JWT claim tenant_id
        if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out Guid tenantId))
            return await tenantService.GetByIdAsync(tenantId);

        // Priority 2: X-Tenant-ID header
        if (context.Request.Headers.TryGetValue(TenantHeader, out var headerValue) && !string.IsNullOrEmpty(headerValue))
            return await tenantService.GetBySlugAsync(headerValue!);

        // Priority 3: Subdomain
        string? subdomain = ExtractSubdomain(context.Request.Host.Host);
        if (!string.IsNullOrEmpty(subdomain))
            return await tenantService.GetBySlugAsync(subdomain);

        return null;
    }

    private static string? ExtractSubdomain(string host)
    {
        // Strip port
        host = host.Split(':')[0];
        string[] parts = host.Split('.');

        // "acme.yourapp.com" → 3 parts → "acme"
        // "localhost" or "yourapp.com" → no subdomain
        return parts.Length >= 3 ? parts[0] : null;
    }
}
