namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Features;

/// <summary>Marks a request that runs only when every named feature is enabled for the caller's tenant.</summary>
public interface IFeatureRequest
{
    string[] RequiredFeatures { get; }
}
