using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Constants;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Context;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Entities;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Exceptions;

namespace NetCoreBackend.NArchitecture.Core.MultiTenancy.Middleware;

public class TenantMiddleware
{
    private const string TenantHeader = "X-Tenant-ID";

    // Subdomains that must never resolve to a tenant slug, regardless of DB content.
    // Prevents www.app.com / api.app.com / cdn.app.com from being treated as tenants
    // named "www" / "api" / "cdn".
    private static readonly HashSet<string> ReservedSubdomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "api", "app", "admin", "cdn", "static", "assets", "mail", "smtp", "ftp"
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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

        Tenant? tenant;
        try
        {
            tenant = await ResolveTenantAsync(context, tenantService, tenantIdClaim);
        }
        catch (TenantConflictException ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync(ex.Message);
            return;
        }

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

    private async Task<Tenant?> ResolveTenantAsync(HttpContext context, ITenantService tenantService, string? tenantIdClaim)
    {
        bool hasHeader = context.Request.Headers.TryGetValue(TenantHeader, out var headerValue)
            && !string.IsNullOrEmpty(headerValue);

        // Priority 1: JWT claim wins, but if header is ALSO present we verify they agree.
        // A mismatch is a 400 — the client is sending contradictory tenant identifiers.
        if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out Guid tenantId))
        {
            Tenant? tenantFromClaim = await tenantService.GetByIdAsync(tenantId);
            if (hasHeader && tenantFromClaim != null
                && !string.Equals(headerValue.ToString(), tenantFromClaim.Identifier, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantConflictException(
                    $"X-Tenant-ID header ('{headerValue}') does not match the tenant in the access token.");
            }
            return tenantFromClaim;
        }

        // Priority 2: X-Tenant-ID header (typically pre-login or service-to-service)
        if (hasHeader)
        {
            Tenant? tenant = await tenantService.GetBySlugAsync(headerValue!);
            if (tenant == null)
                _logger.LogWarning("Tenant slug from header did not resolve: {Slug}", headerValue.ToString());
            return tenant;
        }

        // Priority 3: Subdomain
        string? subdomain = ExtractSubdomain(context.Request.Host.Host);
        if (!string.IsNullOrEmpty(subdomain))
        {
            Tenant? tenant = await tenantService.GetBySlugAsync(subdomain);
            if (tenant == null)
                _logger.LogWarning("Subdomain did not resolve to a tenant: {Subdomain}", subdomain);
            return tenant;
        }

        return null;
    }

    internal static string? ExtractSubdomain(string host)
    {
        // Bracketed IPv6 literal: "[::1]" or "[::1]:5000". Trim the brackets/port before
        // any further parsing — splitting on ':' would corrupt the address.
        if (host.StartsWith('['))
        {
            int close = host.IndexOf(']');
            if (close < 0) return null;
            host = host.Substring(1, close - 1);
        }
        else
        {
            // Strip port (Host.Host should not include port, but defend anyway)
            int colon = host.IndexOf(':');
            if (colon >= 0) host = host[..colon];
        }

        // Numeric IPs never carry tenant info — "192.168.1.1" must not yield "192"
        if (IPAddress.TryParse(host, out _)) return null;

        string[] parts = host.Split('.');

        // Need at least 3 parts to have a subdomain: sub.example.com
        // Note: compound TLDs (app.co.uk) are not handled here — apps targeting those
        // should configure tenants via header or JWT instead of subdomain.
        if (parts.Length < 3) return null;

        string subdomain = parts[0];
        if (ReservedSubdomains.Contains(subdomain)) return null;

        return subdomain;
    }
}
