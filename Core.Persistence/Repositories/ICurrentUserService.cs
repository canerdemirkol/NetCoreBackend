namespace NetCoreBackend.NArchitecture.Core.Persistence.Repositories;

/// <summary>
/// Provides the authenticated user's ID for audit field population.
/// Implement in the consuming application (e.g. via IHttpContextAccessor + claim reading)
/// and register as scoped. Returns null when no user is authenticated.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
}
