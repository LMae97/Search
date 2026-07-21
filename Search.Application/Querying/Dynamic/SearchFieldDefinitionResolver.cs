using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Dynamic;

/// <summary>
/// Trasforma una <see cref="SearchFieldDefinition"/> in un <see cref="FieldDescriptor"/> eseguibile,
/// interpretando il <see cref="SearchFieldDefinition.Path"/> secondo il criterio effettivo di binding
/// (che <b>non</b> coincide con relazionale-vs-documentale):
/// <list type="bullet">
/// <item><b>Basato su selettore</b> (solo <see cref="StoreKind.PostgresEF"/>): il path CLR viene ricostruito
/// in un selettore (Expression) — tipo/operatori derivati dal tipo reale della proprietà.</item>
/// <item><b>Basato su storage path</b> (<see cref="StoreKind.PostgresRaw"/> ed <see cref="StoreKind.Mongo"/>):
/// il path diventa <see cref="FieldDescriptor.StoragePath"/> — nessun selettore CLR. Per il Raw è
/// un'espressione-colonna SQL, per Mongo il path del documento.</item>
/// </list>
/// </summary>
public sealed class SearchFieldDefinitionResolver
{
    private readonly SearchEntity _entity;

    public SearchFieldDefinitionResolver(SearchEntity entity) => _entity = entity;

    public FieldDescriptor Resolve(SearchFieldDefinition definition)
    {
        return _entity.Store == StoreKind.PostgresEF
            ? ResolveSelectorBased(definition)     // PostgresEF
            : ResolveStoragePathBased(definition); // PostgresRaw e Mongo
    }

    private FieldDescriptor ResolveSelectorBased(SearchFieldDefinition definition)
    {
        var entityType = _entity.ClrType
            ?? throw new InvalidOperationException($"L'entità relazionale '{_entity.Name}' non ha un tipo CLR.");

        // Path CLR → Expression (valida il path, fail-fast). Per gli array il path proietta una collezione
        // (es. "Tags.Name" → x.Tags.Select(t => t.Name)); il tipo del campo è quello dell'elemento.
        var selector = PropertyPathSelectorFactory.Build(entityType, definition.Path);

        var valueType = definition.IsArray
            ? PropertyPathSelectorFactory.GetEnumerableElementType(selector.ReturnType)
                ?? throw new InvalidOperationException(
                    $"Il campo array '{definition.Name}' non proietta una collezione (path '{definition.Path}').")
            : selector.ReturnType;

        var (kind, underlying) = FieldKindResolver.Resolve(valueType);

        return FieldDescriptor.BuildSelectorBased(
            selector: selector,
            name: definition.Name,
            kind: kind,
            isArray: definition.IsArray,
            clrType: underlying,
            label: definition.Label,
            section: definition.Section,
            defaultOrder: definition.DefaultOrder,
            isHidden: definition.IsHidden,
            requiredPermissionId: definition.RequiredPermissionId,
            allowedOperators: OperatorRules.DefaultFor(kind, definition.IsArray)
        );
    }

    // Copre sia Mongo sia PostgresRaw: il campo porta un path esplicito (StoragePath), non un selettore CLR.
    private static FieldDescriptor ResolveStoragePathBased(SearchFieldDefinition definition)
    {
        // Nessun tipo CLR reale: usiamo la Kind dichiarata (solo per la coercizione dei valori del filtro).
        var clrType = ClrTypeFor(definition.Kind);

        return FieldDescriptor.BuildPathBased(
            storagePath: definition.Path,
            name: definition.Name,
            kind: definition.Kind,
            isArray: definition.IsArray,
            clrType: clrType,
            label: definition.Label ?? definition.Name,
            section: definition.Section,
            defaultOrder: definition.DefaultOrder,
            isHidden: definition.IsHidden,
            requiredPermissionId: definition.RequiredPermissionId,
            allowedOperators: OperatorRules.DefaultFor(definition.Kind, definition.IsArray)
        );
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
