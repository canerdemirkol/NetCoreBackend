using NetCoreBackend.NArchitecture.Core.EntityChanges.Entities;
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
