using Microsoft.EntityFrameworkCore;
using Moq;
using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Tests.Persistence;

// Regression coverage for the R1 cascade fixes:
//   - EditEntityPropertiesToAdd hard-errors when TenantSetter is missing for a tenant entity.
//   - GetRelationLoaderQuery enforces tenant ownership on lazy-loaded relations.
//
// The tests deliberately exercise tenant entities through EfRepositoryBase with the in-memory
// provider. In-memory honors global query filters configured on the model but does NOT
// enforce relational constraints — that's fine here because we're proving the C# guards run,
// not the SQL semantics.
public sealed class TenantCascadeTests
{
    private sealed class FakeDbContext : DbContext
    {
        public DbSet<Order> Orders => Set<Order>();

        public FakeDbContext(DbContextOptions<FakeDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(b =>
            {
                b.HasKey(o => o.Id);
                b.HasQueryFilter(o => !o.DeletedDate.HasValue);
            });
        }
    }

    private sealed class Order : TenantEntity<Guid>
    {
        public string Sku { get; set; } = string.Empty;
    }

    private sealed class OrderRepository : EfRepositoryBase<Order, Guid, FakeDbContext>
    {
        public OrderRepository(FakeDbContext context, ITenantEntitySetter? tenantSetter)
            : base(context, tenantSetter) { }
    }

    private static FakeDbContext NewContext() =>
        new(new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase($"cascade-{Guid.NewGuid()}")
            .Options);

    [Fact]
    public async Task AddAsync_TenantEntity_NoTenantSetter_ThrowsLoudly()
    {
        await using FakeDbContext ctx = NewContext();
        OrderRepository repo = new(ctx, tenantSetter: null);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.AddAsync(new Order { Id = Guid.NewGuid(), Sku = "ABC" }));

        Assert.Contains("ITenantEntitySetter is not registered", ex.Message);
    }

    [Fact]
    public async Task AddAsync_TenantEntity_WithTenantSetter_StampsTenantId()
    {
        Guid tenantId = Guid.NewGuid();
        Mock<ITenantEntitySetter> setter = new();
        setter.SetupGet(s => s.CurrentTenantId).Returns(tenantId);
        setter.SetupGet(s => s.IsSuperAdmin).Returns(false);
        setter.Setup(s => s.SetTenantId(It.IsAny<ITenantEntity>()))
            .Callback<ITenantEntity>(e => e.TenantId = tenantId);

        await using FakeDbContext ctx = NewContext();
        OrderRepository repo = new(ctx, setter.Object);

        Order order = new() { Id = Guid.NewGuid(), Sku = "XYZ" };
        await repo.AddAsync(order);

        Assert.Equal(tenantId, order.TenantId);
        setter.Verify(s => s.SetTenantId(order), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_CrossTenantId_IsRejected()
    {
        // Seed under tenant A.
        Guid tenantA = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();
        await using (FakeDbContext seed = NewContext())
        {
            seed.Orders.Add(new Order { Id = orderId, TenantId = tenantA, Sku = "ORIG" });
            await seed.SaveChangesAsync();

            // Repository now scoped to tenant B with an entity whose Id was minted under A.
            Guid tenantB = Guid.NewGuid();
            Mock<ITenantEntitySetter> setterB = new();
            setterB.SetupGet(s => s.CurrentTenantId).Returns(tenantB);
            setterB.SetupGet(s => s.IsSuperAdmin).Returns(false);

            OrderRepository repoB = new(seed, setterB.Object);
            Order foreignPayload = new() { Id = orderId, TenantId = tenantA, Sku = "PWNED" };

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repoB.UpdateAsync(foreignPayload));

            Assert.Contains("Cross-tenant write blocked", ex.Message);
        }
    }
}
