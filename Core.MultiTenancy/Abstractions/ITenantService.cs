using NetCoreBackend.NArchitecture.Core.MultiTenancy.Entities;

namespace NetCoreBackend.NArchitecture.Core.MultiTenancy.Abstractions;

public interface ITenantService
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default);
}
