namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Features;

/// <summary>
/// Answers what a tenant's package allows (a limit or an on/off module). The app owns the definitions, the editions and the
/// resolution order (typically tenant override › edition › default); Core only asks. The tenant is explicit so background
/// work and limit checks can ask without a request context.
/// </summary>
public interface IFeatureChecker
{
    Task<string> GetValueAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default);
}
