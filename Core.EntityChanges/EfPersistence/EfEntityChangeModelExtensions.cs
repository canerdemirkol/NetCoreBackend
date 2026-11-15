using Microsoft.EntityFrameworkCore;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Entities;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Values;

namespace NetCoreBackend.NArchitecture.Core.EntityChanges.EfPersistence;

/// <summary>
/// Configures the <see cref="EntityChange"/> table. Call from the consumer's <c>DbContext.OnModelCreating</c>; the
/// migration stays with the consumer. No query filter is added: who may read which rows (tenant visibility) is the
/// consumer's rule, and a filter here would replace the consumer's own.
/// </summary>
public static class EfEntityChangeModelExtensions
{
    private const int ImpersonatorTypeLength = 20;
    private const int CorrelationIdLength = 128;
    private const int EntityTypeLength = 64;

    /// <param name="modelBuilder">The model builder, returned for chaining.</param>
    /// <param name="tableName">Override the table name (default: <c>"EntityChanges"</c>).</param>
    public static ModelBuilder ConfigureEntityChanges(this ModelBuilder modelBuilder, string? tableName = "EntityChanges")
    {
        modelBuilder.Entity<EntityChange>(b =>
        {
            b.ToTable(tableName!);
            b.HasKey(c => c.Id);
            b.Property(c => c.Id).ValueGeneratedOnAdd();
            b.Property(c => c.ActorType).HasConversion<short>();
            b.Property(c => c.ImpersonationKind).HasConversion<short>();
            b.Property(c => c.ChangeType).HasConversion<short>();
            b.Property(c => c.ImpersonatorType).HasMaxLength(ImpersonatorTypeLength);
            b.Property(c => c.CorrelationId).HasMaxLength(CorrelationIdLength);
            b.Property(c => c.EntityType).HasMaxLength(EntityTypeLength).IsRequired();
            b.Property(c => c.EntityId).HasMaxLength(EntityChangeValues.MaxEntityIdLength).IsRequired();
            // Bounded by the allow list: a handful of properties, each value at most 512 characters.
            b.Property(c => c.PropertyChanges).IsRequired();

            // The scope's list, newest first.
            b.HasIndex(c => new { c.TenantId, c.CreatedDate });
            // One record's history.
            b.HasIndex(c => new { c.TenantId, c.EntityType, c.EntityId });
            // Retention deletes by age across every scope; the composite indexes cannot serve that range alone.
            b.HasIndex(c => c.CreatedDate);
        });
        return modelBuilder;
    }
}
