using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NetCoreBackend.NArchitecture.Core.EntityChanges.EfPersistence;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Entities;

namespace NetCoreBackend.NArchitecture.Core.Test.EntityChanges;

/// <summary>
/// The table the consumer gets from ConfigureEntityChanges: bounded columns, the three indexes reads and retention
/// rely on, and no query filter (visibility is the consumer's tenant rule, which a framework filter would replace).
/// </summary>
public sealed class EfEntityChangeModelExtensionsTests
{
    private sealed class TestDb : DbContext
    {
        public TestDb(DbContextOptions<TestDb> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb) => mb.ConfigureEntityChanges();
    }

    private static IEntityType EntityType()
    {
        using var db = new TestDb(new DbContextOptionsBuilder<TestDb>().UseInMemoryDatabase($"change-model-{Guid.NewGuid()}").Options);
        return db.Model.FindEntityType(typeof(EntityChange))!;
    }

    [Fact]
    public void TheTable_HasBoundedColumns_AndShortEnums()
    {
        IEntityType type = EntityType();

        Assert.Equal("EntityChanges", type.GetTableName());
        Assert.Equal(20, type.FindProperty(nameof(EntityChange.ImpersonatorType))!.GetMaxLength());
        Assert.Equal(128, type.FindProperty(nameof(EntityChange.CorrelationId))!.GetMaxLength());
        Assert.Equal(64, type.FindProperty(nameof(EntityChange.EntityType))!.GetMaxLength());
        Assert.Equal(128, type.FindProperty(nameof(EntityChange.EntityId))!.GetMaxLength());
        Assert.False(type.FindProperty(nameof(EntityChange.PropertyChanges))!.IsNullable);
        Assert.Equal(typeof(short), type.FindProperty(nameof(EntityChange.ChangeType))!.GetProviderClrType());
        Assert.Equal(typeof(short), type.FindProperty(nameof(EntityChange.ActorType))!.GetProviderClrType());
        Assert.Equal(typeof(short), type.FindProperty(nameof(EntityChange.ImpersonationKind))!.GetProviderClrType());
    }

    [Fact]
    public void TheIndexes_KeepTheirNames()
    {
        Assert.Equal(
            ["IX_EntityChanges_CreatedDate", "IX_EntityChanges_TenantId_CreatedDate", "IX_EntityChanges_TenantId_EntityType_EntityId"],
            EntityType().GetIndexes().Select(i => i.GetDatabaseName()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NoQueryFilter_IsAdded() => Assert.Empty(EntityType().GetDeclaredQueryFilters());
}
