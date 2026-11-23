using NetCoreBackend.NArchitecture.Core.EntityChanges.Abstractions;

namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Model;

/// <summary>A configured entity as the model holds it: stored name, CLR type and its allow-listed properties.</summary>
public sealed record EntityChangeTypeInfo(string Name, Type ClrType, IReadOnlyList<EntityChangeProperty> Properties);
