using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Dynamic;

/// <summary>
/// Trasforma una <see cref="SearchFieldDefinition"/> in un <see cref="FieldDescriptor"/> eseguibile,
/// interpretando il <see cref="SearchFieldDefinition.Path"/> in base allo store dell'entità:
/// <list type="bullet">
/// <item><b>Relazionale</b>: il path CLR viene ricostruito in un selettore (Expression) — tipo/operatori
/// derivati dal tipo reale della proprietà (più robusto della colonna Kind).</item>
/// <item><b>Documentale</b>: il path diventa <see cref="FieldDescriptor.StoragePath"/> — nessun selettore.</item>
/// </list>
/// </summary>
public sealed class SearchFieldDefinitionResolver
{
    private readonly SearchEntityRegistry _entities;

    public SearchFieldDefinitionResolver(SearchEntityRegistry entities) => _entities = entities;

    public FieldDescriptor Resolve(SearchFieldDefinition definition)
    {
        var entity = _entities.Get(definition.EntityName);

        return entity.Store == StoreKind.Postgres
            ? ResolveRelational(definition, entity)
            : ResolveDocument(definition);
    }

    private static FieldDescriptor ResolveRelational(SearchFieldDefinition definition, SearchEntity entity)
    {
        var entityType = entity.ClrType
            ?? throw new InvalidOperationException($"L'entità relazionale '{entity.Name}' non ha un tipo CLR.");

        // Path CLR → Expression (valida il path, fail-fast). Per gli array il path proietta una collezione
        // (es. "Tags.Name" → x.Tags.Select(t => t.Name)); il tipo del campo è quello dell'elemento.
        var selector = PropertyPathSelectorFactory.Build(entityType, definition.Path);

        var valueType = definition.IsArray
            ? PropertyPathSelectorFactory.GetEnumerableElementType(selector.ReturnType)
                ?? throw new InvalidOperationException(
                    $"Il campo array '{definition.Name}' non proietta una collezione (path '{definition.Path}').")
            : selector.ReturnType;

        var (kind, underlying) = FieldKindResolver.Resolve(valueType);

        return new FieldDescriptor(definition.Name, kind, definition.IsArray, underlying, selector,
            OperatorRules.DefaultFor(kind, definition.IsArray))
        {
            Label = definition.Label ?? definition.Name,
            Section = definition.Section,
            VisibleByDefault = definition.VisibleByDefault,
            RequiredPermissionId = definition.RequiredPermissionId
        };
    }

    private static FieldDescriptor ResolveDocument(SearchFieldDefinition definition)
    {
        // Nessun tipo CLR reale: usiamo la Kind dichiarata (solo per la coercizione dei valori del filtro).
        var clrType = ClrTypeFor(definition.Kind);

        return new FieldDescriptor(definition.Name, definition.Kind, definition.IsArray, clrType, selector: null,
            OperatorRules.DefaultFor(definition.Kind, definition.IsArray))
        {
            StoragePath = definition.Path,
            Label = definition.Label ?? definition.Name,
            Section = definition.Section,
            VisibleByDefault = definition.VisibleByDefault,
            RequiredPermissionId = definition.RequiredPermissionId
        };
    }

    private static Type ClrTypeFor(FieldKind kind) => kind switch
    {
        FieldKind.String => typeof(string),
        FieldKind.Integer => typeof(long),
        FieldKind.Decimal => typeof(decimal),
        FieldKind.Boolean => typeof(bool),
        FieldKind.DateTime => typeof(DateTimeOffset),
        FieldKind.Guid => typeof(Guid),
        FieldKind.Enum => typeof(string), // "enum" documentale senza tipo CLR: trattato come stringa
        _ => typeof(string)
    };
}
