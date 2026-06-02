namespace NetCoreBackend.NArchitecture.Core.Persistence.Dynamic;

// Marks a property as off-limits for dynamic filter/sort. Apply to columns that should
// never be exposed via the generic query API (PasswordHash, PasswordSalt, internal
// audit fields, etc.).
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class NotFilterableAttribute : Attribute
{
}
