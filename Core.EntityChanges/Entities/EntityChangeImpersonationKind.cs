namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Entities;

/// <summary>Whether the actor was impersonating someone. The numbers are stored; they never move.</summary>
public enum EntityChangeImpersonationKind : short
{
    None = 0,
    User = 1,
    Tenant = 2,
}
