namespace NetCoreBackend.NArchitecture.Core.Application.Responses;

/// <summary>
/// Marker interface for handler response models. Mirrors <see cref="Dtos.IDto"/> on the
/// outbound side and exists so analyzers / reflection-based mapping can scope themselves
/// to response types.
/// </summary>
public interface IResponse { }
