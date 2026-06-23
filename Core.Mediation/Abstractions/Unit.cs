namespace NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

// Void command'ların "değeri olmayan" dönüşü. struct çünkü tek bir değeri var.
public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Value = default;
    public bool Equals(Unit other) => true;
    public override bool Equals(object? obj) => obj is Unit;
    public override int GetHashCode() => 0;
}
