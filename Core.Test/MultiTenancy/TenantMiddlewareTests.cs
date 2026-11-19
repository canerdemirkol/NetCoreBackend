using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Constants;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Context;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Entities;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Middleware;

namespace Core.Test.MultiTenancy;

/// <summary>An inactive tenant is refused as a ProblemDetails a client can read (code TenantInactive), not as plain text.</summary>
public class TenantMiddlewareTests
{
    [Fact]
    public async Task AnInactiveTenant_Is403ProblemDetails_WithTheTenantInactiveCode()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Acme", Identifier = "acme", IsActive = false };
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(TenantClaimTypes.TenantId, tenant.Id.ToString())], "test")),
        };
        context.Response.Body = new MemoryStream();
        bool nextCalled = false;
        var middleware = new TenantMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<TenantMiddleware>.Instance);

        await middleware.InvokeAsync(context, new SingleTenantService(tenant), new TenantContext());

        string body = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.Contains("\"code\":\"TenantInactive\"", body);
        Assert.Contains("Tenant 'acme' is not active.", body);
    }

    private sealed class SingleTenantService(Tenant tenant) : ITenantService
    {
        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Tenant?>(id == tenant.Id ? tenant : null);
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default) => Task.FromResult<Tenant?>(null);
    }
}
