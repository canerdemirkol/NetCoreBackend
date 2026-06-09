namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.Abstractions;

/// <summary>Provides read access to the correlation ID for the current request or operation.</summary>
public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
}
