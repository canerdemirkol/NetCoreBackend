using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Abstractions;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Collection;

namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Model;

/// <summary>
/// The change log's model, built once (keep it in a static field) from the consumer's configuration classes. Building
/// refuses a configuration that could record the wrong scope or record, or an ambiguous property, so a mistake stops
/// the application at startup instead of writing wrong history. <see cref="Collect"/> is called from the consumer's
/// SaveChangesAsync, after its guards and stamping and before the base save, so the rows commit with the change.
/// </summary>
public sealed class EntityChangeModel
{
    private readonly Dictionary<Type, TrackedType> _byType;

    private EntityChangeModel(IEnumerable<TrackedType> types)
    {
        _byType = types.ToDictionary(t => t.Info.ClrType);
        Types = _byType.Values.Select(t => t.Info).OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        Names = Types.Select(t => t.Name).ToList();
    }

    public IReadOnlyList<EntityChangeTypeInfo> Types { get; }

    public IReadOnlyCollection<string> Names { get; }

    /// <summary>Every public, non-abstract configuration class with a parameterless constructor in the assemblies.</summary>
    public static EntityChangeModel FromAssemblies(params Assembly[] assemblies) =>
        FromConfigurations(assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
                && (type.IsPublic || type.IsNestedPublic)
                && ConfiguredEntityOf(type) is not null
                && type.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => Activator.CreateInstance(type)!)
            .ToArray());

    /// <summary>The given configuration instances (each an <see cref="IEntityChangeConfiguration{TEntity}"/>).</summary>
    public static EntityChangeModel FromConfigurations(params object[] configurations)
    {
        var types = new List<TrackedType>();
        foreach (object configuration in configurations)
        {
            Type entityType = ConfiguredEntityOf(configuration.GetType())
                ?? throw new ArgumentException($"{configuration.GetType().Name} is not an entity change configuration.", nameof(configurations));
            TrackedType built = Build(entityType, configuration);
            if (types.Any(t => t.Info.ClrType == entityType))
                throw new InvalidOperationException($"Entity change configuration: {entityType.Name} is configured more than once.");
            if (types.Any(t => t.Info.Name == built.Info.Name))
                throw new InvalidOperationException($"Entity change configuration: the name '{built.Info.Name}' is used more than once.");
            types.Add(built);
        }
        return new EntityChangeModel(types);
    }

    /// <summary>The changes of one save, in tracker order; records without a configuration are skipped.</summary>
    public IReadOnlyList<CollectedEntityChange> Collect(IEnumerable<EntityEntry> entries) => EntityChangeCollector.Collect(entries, _byType);

    private static Type? ConfiguredEntityOf(Type type) =>
        type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityChangeConfiguration<>))
            ?.GetGenericArguments()[0];

    private static TrackedType Build(Type entityType, object configuration)
    {
        MethodInfo build = typeof(EntityChangeModel).GetMethod(nameof(BuildTyped), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityType);
        try
        {
            return (TrackedType)build.Invoke(null, [configuration])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static TrackedType BuildTyped<TEntity>(IEntityChangeConfiguration<TEntity> configuration) where TEntity : class
    {
        var builder = new EntityChangeTypeBuilder<TEntity>();
        configuration.Configure(builder);
        string entity = typeof(TEntity).Name;

        if (string.IsNullOrWhiteSpace(builder.Name))
            throw new InvalidOperationException($"Entity change configuration of {entity}: HasName is required.");
        if (builder.Scope is null)
            throw new InvalidOperationException($"Entity change configuration of {entity}: ScopeBy is required.");
        if (builder.Id is null)
            throw new InvalidOperationException($"Entity change configuration of {entity}: IdBy is required.");
        if (builder.Properties.Count == 0)
            throw new InvalidOperationException($"Entity change configuration of {entity}: name at least one property with Track or Mask.");
        string? repeated = builder.Properties.GroupBy(p => p.Name).FirstOrDefault(g => g.Count() > 1)?.Key;
        if (repeated is not null)
            throw new InvalidOperationException($"Entity change configuration of {entity}: '{repeated}' is named more than once (Track and Mask exclude each other).");

        Func<TEntity, Guid?> scope = builder.Scope;
        Func<TEntity, string> id = builder.Id;
        Func<TEntity, bool> follow = builder.Follow;
        Func<TEntity, bool> maskAll = builder.MaskAll;
        return new TrackedType(
            new EntityChangeTypeInfo(builder.Name, typeof(TEntity), builder.Properties.ToList()),
            e => scope((TEntity)e), e => id((TEntity)e), e => follow((TEntity)e), e => maskAll((TEntity)e));
    }
}
