using Microsoft.Extensions.DependencyInjection;
using Moq;
using NetCoreBackend.NArchitecture.Core.Application.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Application.Pipelines.Features;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;
using NetCoreBackend.NArchitecture.Core.Mediation.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;

namespace NetCoreBackend.NArchitecture.Core.Test.Application;

/// <summary>A request that names features runs only when the caller's tenant has all of them (edition / tenant override).</summary>
public sealed class FeatureRequirementBehaviorTests
{
    private const string AuditLogs = "App.AuditLogs";
    private const string OrganizationUnits = "App.OrganizationUnits";
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly Mock<IFeatureChecker> _features = new();

    private static Mock<ITenantContext> Tenant(Guid? tenantId, bool isSuperAdmin = false, bool isImpersonating = false)
    {
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(t => t.TenantId).Returns(tenantId);
        tenant.SetupGet(t => t.HasTenant).Returns(tenantId.HasValue);
        tenant.SetupGet(t => t.IsSuperAdmin).Returns(isSuperAdmin);
        tenant.SetupGet(t => t.IsImpersonating).Returns(isImpersonating);
        return tenant;
    }

    private void Feature(string name, string value) =>
        _features.Setup(f => f.GetValueAsync(TenantId, name, It.IsAny<CancellationToken>())).ReturnsAsync(value);

    private async Task<bool> RunAsync(Mock<ITenantContext> tenant, params string[] requiredFeatures)
    {
        bool handled = false;
        var behavior = new FeatureRequirementBehavior<FeatureGatedRequest, string>(_features.Object, tenant.Object);
        await behavior.Handle(new FeatureGatedRequest(requiredFeatures), _ => { handled = true; return Task.FromResult("ok"); }, CancellationToken.None);
        return handled;
    }

    [Fact]
    public async Task AnEnabledFeature_LetsTheRequestThrough()
    {
        Feature(AuditLogs, "true");

        Assert.True(await RunAsync(Tenant(TenantId), AuditLogs));
    }

    [Fact]
    public async Task ADisabledFeature_Is403_AndTheHandlerDoesNotRun()
    {
        Feature(AuditLogs, "false");
        bool handled = false;
        var behavior = new FeatureRequirementBehavior<FeatureGatedRequest, string>(_features.Object, Tenant(TenantId).Object);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => behavior.Handle(
            new FeatureGatedRequest([AuditLogs]), _ => { handled = true; return Task.FromResult("ok"); }, CancellationToken.None));

        Assert.False(handled);
        Assert.Contains(AuditLogs, error.Message);
    }

    [Fact]
    public async Task EveryNamedFeatureMustBeEnabled()
    {
        Feature(AuditLogs, "true");
        Feature(OrganizationUnits, "false");

        await Assert.ThrowsAsync<ForbiddenException>(() => RunAsync(Tenant(TenantId), AuditLogs, OrganizationUnits));
    }

    // The host has no edition; features describe a tenant's package.
    [Fact]
    public async Task TheHost_IsNotChecked()
    {
        Assert.True(await RunAsync(Tenant(null, isSuperAdmin: true), AuditLogs));
        _features.VerifyNoOtherCalls();
    }

    // The host impersonating a tenant sees what the tenant sees.
    [Fact]
    public async Task TheHostImpersonatingATenant_GetsTheTenantsFeatures()
    {
        Feature(AuditLogs, "false");

        await Assert.ThrowsAsync<ForbiddenException>(() => RunAsync(Tenant(TenantId, isSuperAdmin: true, isImpersonating: true), AuditLogs));
    }

    [Fact]
    public async Task NoTenant_Is403()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => RunAsync(Tenant(null), AuditLogs));
    }

    [Theory]
    [InlineData("True", true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("", false)]
    public async Task OnlyTrueEnablesAFeature(string value, bool enabled)
    {
        Feature(AuditLogs, value);

        if (enabled)
            Assert.True(await RunAsync(Tenant(TenantId), AuditLogs));
        else
            await Assert.ThrowsAsync<ForbiddenException>(() => RunAsync(Tenant(TenantId), AuditLogs));
    }

    // Apps that never mark a request need not register an IFeatureChecker.
    [Fact]
    public async Task AnUnmarkedRequest_RunsWithoutAFeatureChecker()
    {
        using ServiceProvider provider = Services(featureChecker: null).BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        string result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new PlainRequest());

        Assert.Equal("plain", result);
    }

    [Fact]
    public async Task TheRegistration_AppliesTheBehaviorToMarkedRequests()
    {
        Feature(AuditLogs, "false");
        using ServiceProvider provider = Services(_features.Object).BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            scope.ServiceProvider.GetRequiredService<IMediator>().Send(new FeatureGatedRequest([AuditLogs])));
    }

    private static ServiceCollection Services(IFeatureChecker? featureChecker)
    {
        var services = new ServiceCollection();
        services.AddMediator(typeof(FeatureRequirementBehaviorTests).Assembly);
        services.AddNArchitecturePipelineBehaviors();
        services.AddSingleton(Tenant(TenantId).Object);
        if (featureChecker is not null)
            services.AddSingleton(featureChecker);
        return services;
    }

    public sealed record FeatureGatedRequest(string[] RequiredFeatures) : IRequest<string>, IFeatureRequest;

    public sealed class FeatureGatedHandler : IRequestHandler<FeatureGatedRequest, string>
    {
        public Task<string> Handle(FeatureGatedRequest request, CancellationToken cancellationToken) => Task.FromResult("ok");
    }

    public sealed record PlainRequest : IRequest<string>;

    public sealed class PlainHandler : IRequestHandler<PlainRequest, string>
    {
        public Task<string> Handle(PlainRequest request, CancellationToken cancellationToken) => Task.FromResult("plain");
    }
}
