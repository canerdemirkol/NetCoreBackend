namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Features;

public static class FeatureCheckerExtensions
{
    /// <summary>On/off features: only "true" (any case) enables.</summary>
    public static async Task<bool> IsEnabledAsync(
        this IFeatureChecker featureChecker, Guid tenantId, string featureName, CancellationToken cancellationToken = default) =>
        string.Equals(await featureChecker.GetValueAsync(tenantId, featureName, cancellationToken), "true", StringComparison.OrdinalIgnoreCase);
}
