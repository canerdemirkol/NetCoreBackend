namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Abstractions;

/// <summary>A property the change log records; a masked one is written as "changed" with no value.</summary>
public sealed record EntityChangeProperty(string Name, bool Masked);
