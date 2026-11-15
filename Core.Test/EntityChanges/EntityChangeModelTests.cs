using NetCoreBackend.NArchitecture.Core.EntityChanges.Abstractions;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Model;
using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Test.EntityChanges;

/// <summary>
/// The model is built once from configuration classes. A configuration that could record the wrong scope, the
/// wrong record or an ambiguous property stops the build with a clear message, so the mistake shows at startup.
/// </summary>
public sealed class EntityChangeModelTests
{
    public sealed class Order : Entity<Guid>
    {
        public Guid TenantId { get; set; }
        public string Number { get; set; } = string.Empty;
        public Address Shipping { get; set; } = new();
    }

    public sealed class Address
    {
        public string City { get; set; } = string.Empty;
    }

    // Public, so the assembly scan finds it; the invalid ones below are private and never scanned.
    public sealed class ScannedOrderChanges : IEntityChangeConfiguration<Order>
    {
        public void Configure(EntityChangeTypeBuilder<Order> builder) =>
            builder.HasName("Order").ScopeBy(o => o.TenantId).IdBy(o => o.Id).Track(o => o.Number);
    }

    private sealed class Configured(Action<EntityChangeTypeBuilder<Order>> configure) : IEntityChangeConfiguration<Order>
    {
        public void Configure(EntityChangeTypeBuilder<Order> builder) => configure(builder);
    }

    private static InvalidOperationException Refused(params IEntityChangeConfiguration<Order>[] configurations) =>
        Assert.Throws<InvalidOperationException>(() => EntityChangeModel.FromConfigurations(configurations));

    [Fact]
    public void TheScan_FindsPublicConfigurations()
    {
        EntityChangeModel model = EntityChangeModel.FromAssemblies(typeof(EntityChangeModelTests).Assembly);

        Assert.Contains("Order", model.Names);
        Assert.DoesNotContain("Customer", model.Names);   // its configuration is private (EntityChangeCollectorTests)
    }

    [Fact]
    public void TheModel_ListsItsTypesAndProperties()
    {
        EntityChangeModel model = EntityChangeModel.FromConfigurations(new ScannedOrderChanges());

        EntityChangeTypeInfo type = Assert.Single(model.Types);
        Assert.Equal("Order", type.Name);
        Assert.Equal(typeof(Order), type.ClrType);
        Assert.Equal(["Number"], type.Properties.Select(p => p.Name));
    }

    [Fact]
    public void AMissingScopeOrId_IsRefused()
    {
        Assert.Contains("ScopeBy", Refused(new Configured(b => b.HasName("Order").IdBy(o => o.Id).Track(o => o.Number))).Message);
        Assert.Contains("IdBy", Refused(new Configured(b => b.HasName("Order").ScopeBy(o => o.TenantId).Track(o => o.Number))).Message);
        Assert.Contains("HasName", Refused(new Configured(b => b.ScopeBy(o => o.TenantId).IdBy(o => o.Id).Track(o => o.Number))).Message);
    }

    [Fact]
    public void ThePropertyBothTrackedAndMasked_IsRefused() =>
        Assert.Contains("Number", Refused(new Configured(b => b.HasName("Order").ScopeBy(o => o.TenantId).IdBy(o => o.Id)
            .Track(o => o.Number).Mask(o => o.Number))).Message);

    [Fact]
    public void ANestedProperty_IsRefused() =>
        Assert.Throws<ArgumentException>(() => EntityChangeModel.FromConfigurations(new Configured(b => b.HasName("Order")
            .ScopeBy(o => o.TenantId).IdBy(o => o.Id).Track(o => o.Shipping.City))));

    private sealed class AddressNamedOrder : IEntityChangeConfiguration<Address>
    {
        public void Configure(EntityChangeTypeBuilder<Address> builder) =>
            builder.HasName("Order").ScopeBy(_ => (Guid?)null).IdBy(a => a.City).Track(a => a.City);
    }

    [Fact]
    public void TheSameType_Twice_IsRefused()
    {
        var first = new Configured(b => b.HasName("Order").ScopeBy(o => o.TenantId).IdBy(o => o.Id).Track(o => o.Number));
        var second = new Configured(b => b.HasName("Order2").ScopeBy(o => o.TenantId).IdBy(o => o.Id).Track(o => o.Number));

        Assert.Contains(nameof(Order), Refused(first, second).Message);
    }

    [Fact]
    public void TheSameName_Twice_IsRefused() =>
        Assert.Contains("Order", Assert.Throws<InvalidOperationException>(() =>
            EntityChangeModel.FromConfigurations(new ScannedOrderChanges(), new AddressNamedOrder())).Message);

    [Fact]
    public void AConfigurationWithNoProperties_IsRefused() =>
        Assert.Contains("Track", Refused(new Configured(b => b.HasName("Order").ScopeBy(o => o.TenantId).IdBy(o => o.Id))).Message);
}
