using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Abstractions;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Entities;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Model;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Values;

namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Collection;

/// <summary>
/// One collected change, before the consumer adds its actor and turns it into an <see cref="EntityChange"/> row.
/// </summary>
public sealed record CollectedEntityChange(
    Guid? TenantId,
    EntityChangeType ChangeType,
    string EntityType,
    string EntityId,
    IReadOnlyList<PropertyChange> Properties);

/// <summary>
/// Reads only the configured properties of configured records. Values are compared, not the modified flags: Update()
/// flags every property, and a save that changes nothing allow-listed leaves no change. A soft delete (DeletedDate
/// set) and a removal are deletions, recorded with the last values.
/// </summary>
internal static class EntityChangeCollector
{
    private const string DeletedDateProperty = "DeletedDate";

    public static IReadOnlyList<CollectedEntityChange> Collect(IEnumerable<EntityEntry> entries, IReadOnlyDictionary<Type, TrackedType> byType)
    {
        var changes = new List<CollectedEntityChange>();
        foreach (EntityEntry entry in entries)
        {
            if (!byType.TryGetValue(entry.Metadata.ClrType, out TrackedType? tracked) || !tracked.IsFollowed(entry.Entity))
                continue;

            (EntityChangeType changeType, List<PropertyChange> properties) = Describe(entry, tracked.Info.Properties, tracked.IsMaskedAll(entry.Entity));
            if (properties.Count == 0)
                continue;

            changes.Add(new CollectedEntityChange(tracked.TenantOf(entry.Entity), changeType, tracked.Info.Name, tracked.IdOf(entry.Entity), properties));
        }
        return changes;
    }

    private static (EntityChangeType, List<PropertyChange>) Describe(EntityEntry entry, IReadOnlyList<EntityChangeProperty> properties, bool maskAll)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                return (EntityChangeType.Created, properties
                    .Select(p => Change(p, maskAll, null, entry.Property(p.Name).CurrentValue)).ToList());
            case EntityState.Deleted:
            case EntityState.Modified when IsSoftDelete(entry):
                return (EntityChangeType.Deleted, properties
                    .Select(p => Change(p, maskAll, entry.Property(p.Name).OriginalValue, null)).ToList());
            case EntityState.Modified:
                return (EntityChangeType.Updated, properties
                    .Select(p => (Property: p, Entry: entry.Property(p.Name)))
                    .Where(x => !Equals(x.Entry.OriginalValue, x.Entry.CurrentValue))
                    .Select(x => Change(x.Property, maskAll, x.Entry.OriginalValue, x.Entry.CurrentValue))
                    .ToList());
            default:
                return (EntityChangeType.Updated, []);
        }
    }

    // A masked property never carries a value, not even a hash of it: the comparison above happens in memory.
    private static PropertyChange Change(EntityChangeProperty property, bool maskAll, object? oldValue, object? newValue) =>
        property.Masked || maskAll
            ? new PropertyChange(property.Name, null, null, Masked: true)
            : new PropertyChange(property.Name, EntityChangeValues.Format(oldValue), EntityChangeValues.Format(newValue));

    private static bool IsSoftDelete(EntityEntry entry)
    {
        PropertyEntry? deletedDate = entry.Properties.FirstOrDefault(p => p.Metadata.Name == DeletedDateProperty);
        return deletedDate is not null && deletedDate.OriginalValue is null && deletedDate.CurrentValue is not null;
    }
}
