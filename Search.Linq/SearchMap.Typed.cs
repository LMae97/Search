using System.Linq.Expressions;
using WeByte.Search.Core.Metadata;

namespace WeByte.Search.Linq;

/// <summary>
/// Entry point del builder type-safe (store LINQ/EF). Non è <c>partial</c> con <c>Search.Core.Fluent.SearchMap</c>
/// (documentale/raw) perché una <c>partial class</c> non può avere parti in assembly diversi: nome distinto
/// così, per chi referenzia sia Search.Mongo/Sql sia Search.Linq nello stesso file, "SearchMap" e "LinqSearchMap"
/// non sono mai ambigui.
/// </summary>
public static class LinqSearchMap
{
    /// <summary>
    /// Mappa <b>type-safe</b> per uno store LINQ/EF a partire dal tipo CLR <typeparamref name="T"/>. Qui il
    /// code-first brilla: il campo si dichiara con un'espressione (<c>p => p.Name</c>), <b>nome e tipo dedotti</b>
    /// dalla proprietà — niente stringhe, niente <see cref="FieldKind"/> a mano, refactor-safe.
    /// <code>
    /// var map = LinqSearchMap.For<Product>()
    ///     .Field(p => p.Name).Searchable()
    ///     .Field(p => p.Price)                       // dedotto: Decimal
    ///     .Field(p => p.Tags.Select(t => t.Name), "tags")
    ///     .Build();
    /// </code>
    /// </summary>
    public static TypedSearchMapBuilder<T> For<T>(string? entityName = null) => new(entityName ?? typeof(T).Name);
}

/// <summary>
/// Builder fluente type-safe. Ogni <see cref="Field{TValue}"/> aggiunge un campo dedotto dall'espressione; i
/// decoratori modificano l'ultimo campo. Le collezioni (<c>IEnumerable<scalare></c>) sono riconosciute in
/// automatico e trattate come campi array.
/// </summary>
public sealed class TypedSearchMapBuilder<T>
{
    private readonly string _entityName;
    private readonly List<FieldSpec> _fields = [];

    internal TypedSearchMapBuilder(string entityName) => _entityName = entityName;

    /// <summary>
    /// Aggiunge un campo dal selettore. Il tipo del campo è dedotto da <typeparamref name="TValue"/> (o dal tipo
    /// elemento se è una collezione). <paramref name="name"/> assente ⇒ dedotto dal nome della proprietà (per le
    /// proiezioni tipo <c>Select(...)</c> va indicato esplicitamente).
    /// </summary>
    public TypedSearchMapBuilder<T> Field<TValue>(Expression<Func<T, TValue>> selector, string? name = null)
    {
        var elementType = PropertyPathSelectorFactory.GetEnumerableElementType(typeof(TValue));
        var isArray = elementType is not null;
        var (kind, underlying) = FieldKindResolver.Resolve(elementType ?? typeof(TValue));

        var fieldName = name ?? MemberName(selector)
            ?? throw new InvalidOperationException(
                "Nome del campo non deducibile dall'espressione: passa un nome esplicito a Field(selector, name).");

        _fields.Add(new FieldSpec(selector, fieldName, kind, underlying, isArray));
        return this;
    }

    /// <summary>Marca l'ultimo campo come parte della ricerca full-text (<c>SearchRequest.Search</c>).</summary>
    public TypedSearchMapBuilder<T> Searchable() => UpdateLastField(f => f with { IsSearchable = true });

    /// <summary>L'ultimo campo è filtrabile/ordinabile ma non proiettabile.</summary>
    public TypedSearchMapBuilder<T> Hidden() => UpdateLastField(f => f with { IsHidden = true });

    /// <summary>Etichetta per il FE dell'ultimo campo.</summary>
    public TypedSearchMapBuilder<T> Label(string label) => UpdateLastField(f => f with { Label = label });

    /// <summary>L'ultimo campo è visibile solo a chi possiede il permesso indicato.</summary>
    public TypedSearchMapBuilder<T> RequiresPermission(Guid permissionId) => UpdateLastField(f => f with { Permission = permissionId });

    /// <summary>Impacchetta i campi in una mappa (i descrittori usano il selettore reale, nessuna stringa di path).</summary>
    public IEntitySearchMap Build()
    {
        var descriptors = _fields.Select(field => FieldDescriptor.BuildSelectorBased(
            selector: field.Selector,
            name: field.Name,
            // Mappa code-first: nessuna definizione a DB da cui prendere un id di risposta distinto,
            // quindi la chiave esposta coincide col nome pubblico del campo.
            responseId: field.Name,
            kind: field.Kind,
            isArray: field.IsArray,
            clrType: field.ClrType,
            jsonColumn: false,
            // Il builder fluente non espone (ancora) i campi Link: nessun riferimento da proiettare.
            linkReferencePath: null,
            linkReferenceEntityId: null,
            label: field.Label,
            section: null,
            defaultOrder: null,
            isHidden: field.IsHidden,
            requiredPermissionId: field.Permission,
            allowedOperators: OperatorRules.DefaultFor(field.Kind, field.IsArray),
            isSearchable: field.IsSearchable));

        return new MaterializedSearchMap(_entityName, descriptors);
    }

    private TypedSearchMapBuilder<T> UpdateLastField(Func<FieldSpec, FieldSpec> change)
    {
        if (_fields.Count == 0)
            throw new InvalidOperationException("Aggiungi un campo con Field(...) prima di decorarlo.");
        _fields[^1] = change(_fields[^1]);
        return this;
    }

    // Nome dedotto solo da un accesso a proprietà (p => p.Name / p => p.Price.Amount). Le proiezioni (Select…)
    // non hanno un nome ovvio → il chiamante lo fornisce.
    private static string? MemberName<TValue>(Expression<Func<T, TValue>> selector)
    {
        var body = selector.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } convert)
            body = convert.Operand; // eventuale boxing verso object
        return body is MemberExpression member ? Camel(member.Member.Name) : null;
    }

    private static string Camel(string name)
        => name.Length > 0 ? char.ToLowerInvariant(name[0]) + name[1..] : name;

    private sealed record FieldSpec(
        LambdaExpression Selector, string Name, FieldKind Kind, Type ClrType, bool IsArray,
        bool IsSearchable = false, bool IsHidden = false, string? Label = null, Guid? Permission = null);
}
