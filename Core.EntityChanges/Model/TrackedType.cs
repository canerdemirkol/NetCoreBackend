namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Model;

/// <summary>A configured entity with its delegates over the untyped tracked instance.</summary>
internal sealed record TrackedType(
    EntityChangeTypeInfo Info,
    Func<object, Guid?> TenantOf,
    Func<object, string> IdOf,
    Func<object, bool> IsFollowed,
    Func<object, bool> IsMaskedAll);
