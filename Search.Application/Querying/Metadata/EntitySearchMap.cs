using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Search.Application.Querying.Filters;

namespace Search.Application.Querying.Metadata;

/// <summary>
/// Base per definire la mappa di ricerca di un'entità in modo dichiarativo:
/// <code>
/// public sealed class ProductSearchMap : EntitySearchMap<Product>
/// {
///     public override string EntityName => "product";
///     public ProductSearchMap()
///     {
///         MapField("price", p => p.Price.Amount);
///         MapArray("tags", p => p.Tags);
///     }
/// }
/// </code>
/// La categoria di tipo e gli operatori ammessi sono inferiti dal selettore; si possono
/// sovrascrivere passando esplicitamente gli operatori.
/// </summary>
public abstract class EntitySearchMap<TEntity> : IEntitySearchMap
{
    private readonly Dictionary<string, FieldDescriptor> _fields = new(StringComparer.OrdinalIgnoreCase);

    public abstract string EntityName { get; }

    public IReadOnlyDictionary<string, FieldDescriptor> Fields => _fields;

    public bool TryGetField(string name, [MaybeNullWhen(false)] out FieldDescriptor descriptor)
        => _fields.TryGetValue(name, out descriptor);

    /// <summary>Mappa un campo scalare.</summary>
    protected void MapField<TField>(
        string name,
        Expression<Func<TEntity, TField>> selector,
        string? label = null,
        string? section = null,
        bool visible = true,
        Guid? requiredPermissionId = null,
        IEnumerable<FilterOperator>? operators = null)
    {
        var (kind, underlying) = ResolveKind(typeof(TField));
        var allowed = operators is null
            ? OperatorRules.DefaultFor(kind, isArray: false)
            : new HashSet<FilterOperator>(operators);
        Register(new FieldDescriptor(name, kind, isArray: false, underlying, selector, allowed)
        {
            Label = label ?? name,
            Section = section,
            VisibleByDefault = visible,
            RequiredPermissionId = requiredPermissionId
        });
    }

    /// <summary>Mappa un campo array/collezione.</summary>
    protected void MapArray<TElement>(
        string name,
        Expression<Func<TEntity, IEnumerable<TElement>>> selector,
        string? label = null,
        string? section = null,
        bool visible = true,
        Guid? requiredPermissionId = null,
        IEnumerable<FilterOperator>? operators = null)
    {
        var (kind, underlying) = ResolveKind(typeof(TElement));
        var allowed = operators is null
            ? OperatorRules.DefaultFor(kind, isArray: true)
            : new HashSet<FilterOperator>(operators);
        Register(new FieldDescriptor(name, kind, isArray: true, underlying, selector, allowed)
        {
            Label = label ?? name,
            Section = section,
            VisibleByDefault = visible,
            RequiredPermissionId = requiredPermissionId
        });
    }

    private void Register(FieldDescriptor descriptor)
    {
        if (!_fields.TryAdd(descriptor.Name, descriptor))
            throw new InvalidOperationException($"Campo '{descriptor.Name}' già mappato per {EntityName}.");
    }

    private static (FieldKind Kind, Type Underlying) ResolveKind(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying.IsEnum) return (FieldKind.Enum, underlying);
        if (underlying == typeof(string)) return (FieldKind.String, underlying);
        if (underlying == typeof(bool)) return (FieldKind.Boolean, underlying);
        if (underlying == typeof(Guid)) return (FieldKind.Guid, underlying);
        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(DateOnly))
            return (FieldKind.DateTime, underlying);
        if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short) || underlying == typeof(byte))
            return (FieldKind.Integer, underlying);
        if (underlying == typeof(decimal) || underlying == typeof(double) || underlying == typeof(float))
            return (FieldKind.Decimal, underlying);

        throw new NotSupportedException($"Tipo di campo non supportato per la ricerca: {type.Name}.");
    }
}
