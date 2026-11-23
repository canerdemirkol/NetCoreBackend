namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Values;

/// <summary>One property of a change row. A masked one (a secret) carries no value, only the fact that it changed.</summary>
public sealed record PropertyChange(string Property, string? OldValue, string? NewValue, bool Masked = false);
