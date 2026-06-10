using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

namespace NetCoreBackend.NArchitecture.Core.Security.Entities;

public class OperationClaim<TId> : AuditableEntity<TId>
{
    public string Name { get; set; }
    public string? Description { get; set; }

    public OperationClaim()
    {
        Name = string.Empty;
    }

    public OperationClaim(string name, string? description = null)
    {
        Name = name;
        Description = description;
    }

    public OperationClaim(TId id, string name, string? description = null)
        : base(id)
    {
        Name = name;
        Description = description;
    }
}
