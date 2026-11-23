using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Entities;

/// <summary>
/// One change log row: which configured record changed, how, by whom, with the old and new values as JSON
/// <c>[{"p","o","n","m"}]</c>. The consumer writes it in the same SaveChanges as the change itself (so both commit or
/// neither does) and guards it as append-only; a retention job deletes rows for good. <see cref="TenantId"/> is the
/// changed record's scope as its configuration defines it (null = the host's own records). Not a tenant entity:
/// host rows have no tenant, and visibility is the consumer's rule.
/// </summary>
public class EntityChange : Entity<Guid>
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public EntityChangeActorType ActorType { get; set; }
    public EntityChangeImpersonationKind ImpersonationKind { get; set; }
    public Guid? ImpersonatorId { get; set; }
    public string? ImpersonatorType { get; set; }
    // Ties the row to the request's other logs; no foreign key.
    public string? CorrelationId { get; set; }
    public EntityChangeType ChangeType { get; set; }
    public string EntityType { get; set; } = default!;
    // The record's id as its configuration renders it (e.g. a setting's name).
    public string EntityId { get; set; } = default!;
    public string PropertyChanges { get; set; } = default!;
}
