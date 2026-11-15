using Microsoft.EntityFrameworkCore;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Abstractions;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Collection;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Entities;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Model;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Values;
using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Test.EntityChanges;

/// <summary>
/// The collector turns one save's tracked changes into change rows: only configured records and properties (an
/// allow list, never "all columns"), values compared rather than modified flags trusted, a soft delete counted as a
/// deletion, and a masked property recorded as "changed" without its value.
/// </summary>
public sealed class EntityChangeCollectorTests
{
    private static readonly Guid TenantA = Guid.NewGuid();

    public sealed class Customer : Entity<Guid>
    {
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Iban { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
    }

    public sealed class Preference : Entity<Guid>
    {
        public Guid? TenantId { get; set; }
        public Guid? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Value { get; set; }
    }

    public sealed class Untracked : Entity<Guid>
    {
        public string Token { get; set; } = string.Empty;
    }

    private sealed class CustomerChanges : IEntityChangeConfiguration<Customer>
    {
        public void Configure(EntityChangeTypeBuilder<Customer> builder) => builder
            .HasName("Customer")
            .ScopeBy(c => c.TenantId)
            .IdBy(c => c.Id)
            .Track(c => c.Name, c => c.Phone)
            .Mask(c => c.Iban);
    }

    private sealed class PreferenceChanges : IEntityChangeConfiguration<Preference>
    {
        public void Configure(EntityChangeTypeBuilder<Preference> builder) => builder
            .HasName("Setting")
            .ScopeBy(p => p.TenantId)
            .IdBy(p => p.Name)
            .FollowWhen(p => p.UserId == null)
            .MaskAllWhen(p => p.Name.EndsWith(".Password", StringComparison.Ordinal))
            .Track(p => p.Value);
    }

    private sealed class TestDb : DbContext
    {
        public TestDb(DbContextOptions<TestDb> opts) : base(opts) { }
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Preference> Preferences => Set<Preference>();
        public DbSet<Untracked> Untrackeds => Set<Untracked>();
    }

    private static readonly EntityChangeModel Model = EntityChangeModel.FromConfigurations(new CustomerChanges(), new PreferenceChanges());

    private static TestDb NewDb(string? name = null) =>
        new(new DbContextOptionsBuilder<TestDb>().UseInMemoryDatabase(name ?? $"changes-{Guid.NewGuid()}").Options);

    private static Customer NewCustomer() => new()
    {
        Id = Guid.NewGuid(), TenantId = TenantA, Name = "Acme", Phone = "+905550000001", Iban = "TR33 0006 1005", PasswordHash = "hash",
    };

    private static async Task<(TestDb Db, Customer Customer)> SavedCustomerAsync()
    {
        TestDb db = NewDb();
        Customer customer = NewCustomer();
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return (db, customer);
    }

    [Fact]
    public void AnAddedRecord_IsCreated_WithItsConfiguredPropertiesInOrder()
    {
        using TestDb db = NewDb();
        Customer customer = NewCustomer();
        db.Customers.Add(customer);

        CollectedEntityChange change = Assert.Single(Model.Collect(db.ChangeTracker.Entries()));

        Assert.Equal(EntityChangeType.Created, change.ChangeType);
        Assert.Equal("Customer", change.EntityType);
        Assert.Equal(customer.Id.ToString(), change.EntityId);
        Assert.Equal(TenantA, change.TenantId);
        Assert.Equal(
            [new PropertyChange("Name", null, "Acme"), new PropertyChange("Phone", null, "+905550000001"), new PropertyChange("Iban", null, null, Masked: true)],
            change.Properties);
    }

    [Fact]
    public async Task AMaskedProperty_SaysItChanged_ButNeverItsValue()
    {
        (TestDb db, Customer customer) = await SavedCustomerAsync();
        customer.Iban = "TR12 0001 0012";

        CollectedEntityChange change = Assert.Single(Model.Collect(db.ChangeTracker.Entries()));
        string json = EntityChangeValues.Serialize(change.Properties);

        Assert.Equal([new PropertyChange("Iban", null, null, Masked: true)], change.Properties);
        Assert.DoesNotContain("TR33", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TR12", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyRealChanges_AreRecorded_AndUnlistedPropertiesNever()
    {
        (TestDb db, Customer customer) = await SavedCustomerAsync();
        customer.Name = "Acme Ltd";
        customer.PasswordHash = "other-hash";
        db.Entry(customer).State = EntityState.Modified;   // Update() flags every property

        CollectedEntityChange change = Assert.Single(Model.Collect(db.ChangeTracker.Entries()));

        Assert.Equal(EntityChangeType.Updated, change.ChangeType);
        Assert.Equal([new PropertyChange("Name", "Acme", "Acme Ltd")], change.Properties);
    }

    [Fact]
    public async Task ASaveThatChangesNothingListed_RecordsNothing()
    {
        (TestDb db, Customer customer) = await SavedCustomerAsync();
        customer.PasswordHash = "other-hash";

        Assert.Empty(Model.Collect(db.ChangeTracker.Entries()));
    }

    [Fact]
    public async Task ASoftDelete_IsADeletion_WithTheLastValues()
    {
        (TestDb db, Customer customer) = await SavedCustomerAsync();
        customer.DeletedDate = DateTime.UtcNow;

        CollectedEntityChange change = Assert.Single(Model.Collect(db.ChangeTracker.Entries()));

        Assert.Equal(EntityChangeType.Deleted, change.ChangeType);
        Assert.Contains(new PropertyChange("Name", "Acme", null), change.Properties);
        Assert.Contains(new PropertyChange("Iban", null, null, Masked: true), change.Properties);
    }

    [Fact]
    public async Task ARemovedRecord_IsADeletion()
    {
        (TestDb db, Customer customer) = await SavedCustomerAsync();
        db.Customers.Remove(customer);

        Assert.Equal(EntityChangeType.Deleted, Assert.Single(Model.Collect(db.ChangeTracker.Entries())).ChangeType);
    }

    [Fact]
    public void ARecordWithoutConfiguration_IsNotRecorded()
    {
        using TestDb db = NewDb();
        db.Untrackeds.Add(new Untracked { Id = Guid.NewGuid(), Token = "secret-token" });

        Assert.Empty(Model.Collect(db.ChangeTracker.Entries()));
    }

    [Fact]
    public void FollowWhen_LeavesOtherRowsOut_AndMaskAllWhen_HidesEveryValue()
    {
        using TestDb db = NewDb();
        db.Preferences.Add(new Preference { Id = Guid.NewGuid(), TenantId = TenantA, UserId = Guid.NewGuid(), Name = "Ui.Theme", Value = "dark" });
        db.Preferences.Add(new Preference { Id = Guid.NewGuid(), TenantId = TenantA, Name = "Smtp.Password", Value = "cipher" });
        db.Preferences.Add(new Preference { Id = Guid.NewGuid(), Name = "Retention.Days", Value = "365" });

        List<CollectedEntityChange> changes = Model.Collect(db.ChangeTracker.Entries()).ToList();

        Assert.Equal(2, changes.Count);
        CollectedEntityChange secret = changes.Single(c => c.EntityId == "Smtp.Password");
        Assert.Equal([new PropertyChange("Value", null, null, Masked: true)], secret.Properties);
        Assert.Equal(TenantA, secret.TenantId);
        CollectedEntityChange plain = changes.Single(c => c.EntityId == "Retention.Days");
        Assert.Equal([new PropertyChange("Value", null, "365")], plain.Properties);
        Assert.Null(plain.TenantId);
    }
}
