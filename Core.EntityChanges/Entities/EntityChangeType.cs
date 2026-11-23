namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Entities;

/// <summary>How a record changed. The numbers are stored; they never move.</summary>
public enum EntityChangeType : short
{
    Created = 0,
    Updated = 1,
    // A soft delete (DeletedDate set) counts as a deletion too.
    Deleted = 2,
}
