namespace NetCoreBackend.NArchitecture.Core.Persistence.Dynamic;

/// <summary>
/// Marks a property as off-limits for the dynamic filter/sort pipeline
/// (<c>IQueryableDynamicFilterExtensions.ToDynamic</c>). Apply to columns that must never
/// be exposed via the generic query API — password hashes/salts, internal audit fields,
/// encrypted blobs, etc.
/// </summary>
/// <remarks>
/// At query-build time the framework walks <c>typeof(TEntity).GetProperties()</c> and
/// rejects any <c>Filter.Field</c> or <c>Sort.Field</c> whose root segment matches an
/// attributed property — even if the consumer's API surface accidentally allows the
/// client to set arbitrary field names.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class NotFilterableAttribute : Attribute
{
}
