namespace NetCoreBackend.NArchitecture.Core.Application.Dtos;

/// <summary>
/// Marker interface for data-transfer objects. Carries no members by design — exists so
/// reflection-based scanning (AutoMapper profiles, FluentValidation auto-registration)
/// can constrain its search to project DTOs.
/// </summary>
public interface IDto { }
