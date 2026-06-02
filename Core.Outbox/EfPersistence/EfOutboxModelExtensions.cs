using Microsoft.EntityFrameworkCore;
using NetCoreBackend.NArchitecture.Core.Outbox.Entities;

namespace NetCoreBackend.NArchitecture.Core.Outbox.EfPersistence;

// Convenience: call from the consumer's DbContext.OnModelCreating so the worker's expected
// schema (indexed by NextAttemptUtc + ProcessedAtUtc for hot-path polling) is in place.
public static class EfOutboxModelExtensions
{
    public static ModelBuilder ConfigureOutbox(this ModelBuilder modelBuilder, string? tableName = "OutboxMessages")
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable(tableName!);
            b.HasKey(m => m.Id);
            b.Property(m => m.EventType).IsRequired().HasMaxLength(256);
            b.Property(m => m.Payload).IsRequired();
            b.Property(m => m.CorrelationId).HasMaxLength(128);

            // Polling hot path: WHERE !IsPoisoned AND ProcessedAtUtc IS NULL AND
            // (NextAttemptUtc IS NULL OR NextAttemptUtc <= now) ORDER BY OccurredAtUtc.
            // Filtered index narrows to the pending set without scanning processed history.
            b.HasIndex(m => new { m.IsPoisoned, m.ProcessedAtUtc, m.NextAttemptUtc, m.OccurredAtUtc })
                .HasDatabaseName("IX_OutboxMessages_DispatchQueue");

            // Archival/cleanup hot path: DELETE … WHERE ProcessedAtUtc < @cutoff.
            // Without this index that query is a full table scan because the dispatch-queue
            // index above is ordered IsPoisoned-first and does not help a ProcessedAtUtc range
            // predicate on processed rows.
            b.HasIndex(m => m.ProcessedAtUtc)
                .HasDatabaseName("IX_OutboxMessages_ProcessedAtUtc");
        });
        return modelBuilder;
    }
}
