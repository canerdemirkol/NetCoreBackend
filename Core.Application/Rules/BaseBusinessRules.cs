namespace NetCoreBackend.NArchitecture.Core.Application.Rules;

/// <summary>
/// Marker base class for feature-level business rule containers. Implementations group
/// invariants (e.g. "Email must be unique within tenant") into a single injectable
/// service so command/query handlers stay thin.
/// </summary>
/// <remarks>
/// <para>
/// Intentionally empty: the consuming application defines its own constructor signature
/// (typically injecting a repository, business-rule-specific localization, etc.) and
/// adds helpers that match its conventions. <see cref="BaseBusinessRules"/> exists to
/// (1) give DI registration a single base type to scan against, and
/// (2) enable test infrastructure such as <c>BaseMockRepository</c> to constrain
/// the generic parameter <c>TBusinessRules : BaseBusinessRules</c>.
/// </para>
/// <para>
/// Do not add framework-wide helpers here without checking consumer impact —
/// any required-parameter constructor introduced here will break every derived class.
/// </para>
/// </remarks>
public abstract class BaseBusinessRules { }
