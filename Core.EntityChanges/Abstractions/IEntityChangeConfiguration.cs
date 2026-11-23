namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Abstractions;

/// <summary>
/// Says which properties of one entity the change log records, in the way <c>IEntityTypeConfiguration&lt;T&gt;</c>
/// configures its table. Classes implementing it are found by an assembly scan (public, with a parameterless
/// constructor). An entity without a configuration is never recorded.
/// </summary>
public interface IEntityChangeConfiguration<TEntity> where TEntity : class
{
    void Configure(EntityChangeTypeBuilder<TEntity> builder);
}
