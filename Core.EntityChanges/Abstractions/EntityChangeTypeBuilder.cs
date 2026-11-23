using System.Linq.Expressions;
using NetCoreBackend.NArchitecture.Core.EntityChanges.Values;

namespace NetCoreBackend.NArchitecture.Core.EntityChanges.Abstractions;

/// <summary>
/// The allow list for one entity. Every property is in one of three states: <see cref="Track"/> (old and new value),
/// <see cref="Mask"/> (only "changed") or absent (never read). There is deliberately no "all properties" option, so a
/// column added later (a hash, a token) is never recorded by default.
/// </summary>
public sealed class EntityChangeTypeBuilder<TEntity> where TEntity : class
{
    private readonly List<EntityChangeProperty> _properties = [];

    internal string? Name { get; private set; }
    internal Func<TEntity, Guid?>? Scope { get; private set; }
    internal Func<TEntity, string>? Id { get; private set; }
    internal Func<TEntity, bool> Follow { get; private set; } = _ => true;
    internal Func<TEntity, bool> MaskAll { get; private set; } = _ => false;
    internal IReadOnlyList<EntityChangeProperty> Properties => _properties;

    /// <summary>The stored entity type name. Given explicitly, so renaming the class never breaks the history.</summary>
    public EntityChangeTypeBuilder<TEntity> HasName(string name)
    {
        Name = name;
        return this;
    }

    /// <summary>The scope (tenant) a change belongs to; null for the host's own records. Required.</summary>
    public EntityChangeTypeBuilder<TEntity> ScopeBy(Func<TEntity, Guid?> scope)
    {
        Scope = scope;
        return this;
    }

    /// <summary>The record's id as stored (a Guid, or a natural key such as a setting's name). Required.</summary>
    public EntityChangeTypeBuilder<TEntity> IdBy<TKey>(Func<TEntity, TKey> id)
    {
        Id = entity => EntityChangeValues.Format(id(entity)) ?? string.Empty;
        return this;
    }

    /// <summary>Properties recorded with their old and new value.</summary>
    public EntityChangeTypeBuilder<TEntity> Track(params Expression<Func<TEntity, object?>>[] properties) => Add(properties, masked: false);

    /// <summary>Properties recorded only as "changed": no value and no hash of it.</summary>
    public EntityChangeTypeBuilder<TEntity> Mask(params Expression<Func<TEntity, object?>>[] properties) => Add(properties, masked: true);

    /// <summary>Only rows matching the predicate are recorded (e.g. not personal preferences).</summary>
    public EntityChangeTypeBuilder<TEntity> FollowWhen(Func<TEntity, bool> predicate)
    {
        Follow = predicate;
        return this;
    }

    /// <summary>Rows matching the predicate have every property masked (e.g. an encrypted setting).</summary>
    public EntityChangeTypeBuilder<TEntity> MaskAllWhen(Func<TEntity, bool> predicate)
    {
        MaskAll = predicate;
        return this;
    }

    private EntityChangeTypeBuilder<TEntity> Add(Expression<Func<TEntity, object?>>[] properties, bool masked)
    {
        foreach (Expression<Func<TEntity, object?>> property in properties)
            _properties.Add(new EntityChangeProperty(PropertyNameOf(property), masked));
        return this;
    }

    // Only a property of the entity itself: x => x.Name. A path (x => x.Address.City) is refused.
    private static string PropertyNameOf(Expression<Func<TEntity, object?>> property)
    {
        Expression body = property.Body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert
            ? convert.Operand
            : property.Body;
        if (body is MemberExpression { Expression: ParameterExpression } member)
            return member.Member.Name;
        throw new ArgumentException($"Entity change configuration of {typeof(TEntity).Name}: '{property}' must name a property of the entity itself.", nameof(property));
    }
}
