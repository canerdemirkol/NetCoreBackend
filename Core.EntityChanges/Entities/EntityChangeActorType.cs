namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Entities;

/// <summary>Who made the change, as the consumer's actor model maps it. The numbers are stored; they never move.</summary>
public enum EntityChangeActorType : short
{
    Anonymous = 0,
    User = 1,
    // A platform admin acting inside a tenant: the id points at the platform admin, not at a tenant user.
    PlatformAdmin = 2,
}
