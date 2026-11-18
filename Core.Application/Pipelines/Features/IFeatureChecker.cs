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

public static class FeatureCheckerExtensions
{
    /// <summary>On/off features: only "true" (any case) enables.</summary>
    public static async Task<bool> IsEnabledAsync(
        this IFeatureChecker featureChecker, Guid tenantId, string featureName, CancellationToken cancellationToken = default) =>
        string.Equals(await featureChecker.GetValueAsync(tenantId, featureName, cancellationToken), "true", StringComparison.OrdinalIgnoreCase);
}
