using Search.Application.Config;
using Search.Core;
using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;
using Search.Core.Dynamic;
using Search.Core.Metadata;
using Search.Core.Validation;
using Search.Application.Dtos;
using Search.Core.Filters;

namespace Search.Application.Querying;

/// <summary>
/// Ingresso unico e store-agnostic per le ricerche: data la chiave-entità delega alla strategia dello store
/// giusto, nascondendo al chiamante se sotto giri SQL grezzo, LINQ/EF o Mongo (Facade).
/// </summary>
public interface ISearchService
{
    SearchResponseDto Search(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller);
}

/// <summary>
/// Strategia di ricerca per uno <b>store</b> (non per una singola entità): sa eseguire qualunque entità di
/// quello store. Il facade smista per <see cref="StoreKind"/> e passa l'entità a runtime.
/// <list type="bullet">
/// <item>Aggiungere uno store = registrare un nuovo handler.</item>
/// <item>Aggiungere un'entità di uno store già coperto = zero codice (Open/Closed).</item>
/// </list>
/// </summary>
public interface ISearchHandler
{
    StoreKind Store { get; }
    SearchResponseDto Search(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller);
}

/// <summary>
/// Base che centralizza la pipeline comune a tutti gli store: mappa effettiva (permessi/tenant) → potatura →
/// validazione. L'unico pezzo store-specifico è <see cref="Execute"/>, implementato dagli handler concreti.
/// </summary>
public abstract class SearchHandlerBase(DbBackedSearchMapProvider maps) : ISearchHandler
{
    /** FLUSSO
     *   SearchHandlerBase.Search
     *     ├─ mappa effettiva → sanitize → validate
     *     ├─ proiezione vuota → DefaultProjection() (unica risoluzione, riusata da dati E header)
     *     ├─ SearchTextExpansion.Apply(config.FreeText, map, request)
     *     │     ├─ OrContains → Search diventa Or(Contains…) dentro Filter, Search=null
     *     │     └─ Atlas      → Search resta intatto
     *     └─ Execute(config, map, prepared, spaceId)
     *           └─ Mongo: se Search ancora valorizzato → $search; altrimenti find (col filtro espanso)
     */
    public abstract StoreKind Store { get; }

    public SearchResponseDto Search(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller)
    {
        var map = maps.GetEffectiveMap(config.SearchEntity, caller);
        var sanitized = new SearchRequestSanitizer(map).Sanitize(request);
        new SearchRequestValidator(map).Validate(sanitized);

        var projection = AdaptProjection(map, config, sanitized);
        var sort = AdaptSort(config, sanitized);

        //TODO: GESTIRE LA PARTE DI AUTORIZZAZIONE QUA SOTTO: VERRA' FATTA PIU' AVANTI, PER ORA NON E' IMPORTANTE
        /*
        var authFilters = config.AuthFilters(new ContractVisibilityFilters {
            AssignedToId = Guid.Parse("021cd22b-2d33-4c5c-ae61-11545048581a"),
            OrgMemberIds = [Guid.Parse("62a91152-9293-4ee7-98bc-c556ad1efad9")]

        });

        var filters = new List<FilterNode?>() { authFilters, sanitized.Filter }.Where(x => x != null).ToList();
        var filterToUse = filters == null || filters.Count == 0 ? null :
            filters.Count == 1 ? filters[0] :
            Filter.And([.. filters!]);
        */
        var filterToUse = sanitized.Filter;

        var resolved = new SearchRequest
        {
            Search = sanitized.Search,
            Filter = filterToUse,
            Projection = projection,
            Sort = sort,
            Page = sanitized.Page
        };

        // Free-text → filtro (OrContains) oppure lasciato allo store (Atlas). Post-validazione, come il tenant scope.
        var prepared = SearchTextExpansion.Apply(config.FreeText, map, resolved);

        var result = Execute(config, map, prepared, caller.SpaceId);

        // Stesso ordine della proiezione risolta (non l'ordine interno di map.Fields, arbitrario/non garantito).
        var prjFields = projection.Select(name => map.Fields[name]);

        return SearchResponseAdapter.ToSearchResponseDto(prjFields, result);
    }

    private static IReadOnlyList<string> AdaptProjection(
        IEntitySearchMap map,
        ISearchableEntityConfig config,
        SearchRequest req
        )
    {
        var basePrj = config.HiddenProjection.ToHashSet();

        var reqPrj = req.Projection;
        var mapPrj = map.DefaultProjection();
        var configPrj = config.DefaultProjection;

        var prj = reqPrj.Count > 0 ? reqPrj
            : mapPrj.Count > 0 ? mapPrj
            : configPrj;

        basePrj.UnionWith(prj.ToHashSet());

        return [.. basePrj];
    }

    private static IReadOnlyList<SortField> AdaptSort(
        ISearchableEntityConfig config, 
        SearchRequest req)
    {
        var defaultSorting = config.IdField;

        var reqSort = req.Sort.ToList();
        var configSort = config.DefaultSort.ToList();

        var sort = reqSort.Count > 0 ? reqSort : configSort;
        if (sort.Any(x => x.Field == defaultSorting)) return sort;

        sort.Add(new SortField(defaultSorting, SortDirection.Ascending));
        return sort;
    }

    /// <summary>Esegue la richiesta già sanificata/validata/espansa contro lo store concreto.</summary>
    protected abstract SearchResult<IReadOnlyDictionary<string, object?>> Execute(ISearchableEntityConfig config, IEntitySearchMap map, SearchRequest request, Guid spaceId);
}

/// <summary>
/// Facade: dal <see cref="SearchEntityRegistry"/> ricava lo store dell'entità e delega all'handler di quello store.
/// </summary>
public sealed class SearchService(IEnumerable<ISearchHandler> handlers) : ISearchService
{
    private readonly IReadOnlyDictionary<StoreKind, ISearchHandler> _handlers =
        handlers.ToDictionary(handler => handler.Store);

    public SearchResponseDto Search(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller)
    {
        var store = config.SearchEntity.Store;
        return _handlers.TryGetValue(store, out var handler)
            ? handler.Search(config, request, caller)
            : throw new InvalidOperationException($"Nessun handler di ricerca registrato per lo store '{store}' (entità '{config.SearchEntity.Name}').");
    }
}
