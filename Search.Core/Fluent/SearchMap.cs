using Search.Core.Dynamic;
using Search.Core.Metadata;

namespace Search.Core.Fluent;

/// <summary>
/// Costruttore <b>code-first</b> di una <see cref="IEntitySearchMap"/>, pensato per il caso "voglio solo filtrare
/// il mio store" senza DB delle definizioni, senza DI, senza config d'entità. È una facciata ergonomica sopra le
/// stesse primitive del percorso data-driven: accumula <see cref="SearchFieldDefinition"/> e le risolve con lo
/// <see cref="SearchFieldDefinitionResolver"/> — quindi tipo/operatori/coercizione sono identici al percorso da DB.
/// <code>
/// var map = SearchMap.ForDocument("contracts")
///     .Field("id",    FieldKind.ObjectId, "_id")
///     .Field("name",  FieldKind.String).Searchable()
///     .Field("price", FieldKind.Decimal)
///     .Build();
/// </code>
/// </summary>
public static class SearchMap
{
    /// <summary>Mappa per uno store documentale (Mongo): i path sono path del documento (dot-notation).</summary>
    public static SearchMapBuilder ForDocument(string entityName) => new(SearchEntity.Document(entityName));

    /// <summary>Mappa per Postgres "raw": i path sono espressioni-colonna SQL già fidate (whitelist).</summary>
    public static SearchMapBuilder ForRelational(string entityName) => new(SearchEntity.RelationalRaw(entityName));
}

/// <summary>
/// Builder fluente di campi. Ogni <see cref="Field"/> aggiunge un campo; i decoratori (<see cref="Searchable"/>,
/// <see cref="Hidden"/>, …) modificano l'ultimo campo aggiunto. <see cref="Build"/> risolve tutto in una mappa.
/// </summary>
public sealed class SearchMapBuilder
{
    private readonly SearchEntity _entity;
    private readonly List<SearchFieldDefinition> _definitions = [];

    internal SearchMapBuilder(SearchEntity entity) => _entity = entity;

    /// <summary>Aggiunge un campo. <paramref name="path"/> assente ⇒ coincide col nome (comune su Mongo).</summary>
    public SearchMapBuilder Field(string name, FieldKind kind, string? path = null, bool isArray = false)
    {
        _definitions.Add(new SearchFieldDefinition(
            _entity.Name, name, kind, JsonColumn: false, isArray, path ?? name));
        return this;
    }

    /// <summary>Marca l'ultimo campo come parte della ricerca full-text (<c>SearchRequest.Search</c>).</summary>
    public SearchMapBuilder Searchable() => UpdateLastField(d => d with { IsSearchable = true });

    /// <summary>L'ultimo campo è filtrabile/ordinabile ma non proiettabile.</summary>
    public SearchMapBuilder Hidden() => UpdateLastField(d => d with { IsHidden = true });

    /// <summary>Etichetta per il FE dell'ultimo campo.</summary>
    public SearchMapBuilder Label(string label) => UpdateLastField(d => d with { Label = label });

    /// <summary>L'ultimo campo è visibile solo a chi possiede il permesso indicato.</summary>
    public SearchMapBuilder RequiresPermission(Guid permissionId) => UpdateLastField(d => d with { RequiredPermissionId = permissionId });

    /// <summary>Risolve i campi in descrittori (stesso resolver del percorso da DB) e li impacchetta in una mappa.</summary>
    public IEntitySearchMap Build()
    {
        var resolver = new SearchFieldDefinitionResolver(_entity);
        return new MaterializedSearchMap(_entity.Name, _definitions.Select(resolver.Resolve));
    }
    private SearchMapBuilder UpdateLastField(Func<SearchFieldDefinition, SearchFieldDefinition> change)
    {
        if (_definitions.Count == 0)
            throw new InvalidOperationException("Aggiungi un campo con Field(...) prima di decorarlo.");
        _definitions[^1] = change(_definitions[^1]);
        return this;
    }
}
