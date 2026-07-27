using Search.Application.Config;
using Search.Core;
using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;
using Search.Core.Dynamic;
using Search.Core.Metadata;
using Search.Core.Validation;

namespace Search.Application.Querying;

/// <summary>
/// Ingresso unico e store-agnostic per le ricerche: data la chiave-entità delega alla strategia dello store
/// giusto, nascondendo al chiamante se sotto giri SQL grezzo, LINQ/EF o Mongo (Facade).
/// </summary>
public interface ISearchService
{
    SearchResult<IReadOnlyDictionary<string, object?>> Search(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller);
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
    SearchResult<IReadOnlyDictionary<string, object?>> Search(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller);
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
     *     ├─ SearchTextExpansion.Apply(config.FreeText, map, request)
     *     │     ├─ OrContains → Search diventa Or(Contains…) dentro Filter, Search=null
     *     │     └─ Atlas      → Search resta intatto
     *     └─ Execute(config, map, prepared, spaceId)
     *           └─ Mongo: se Search ancora valorizzato → $search; altrimenti find (col filtro espanso)
     */
    public abstract StoreKind Store { get; }

    public SearchResult<IReadOnlyDictionary<string, object?>> Search(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller)
    {
        var map = maps.GetEffectiveMap(config.SearchEntity, caller);
        var sanitized = new SearchRequestSanitizer(map).Sanitize(request);
        new SearchRequestValidator(map).Validate(sanitized);

        // Free-text → filtro (OrContains) oppure lasciato allo store (Atlas). Post-validazione, come il tenant scope.
        var prepared = SearchTextExpansion.Apply(config.FreeText, map, sanitized);

        return Execute(config, map, prepared, caller.SpaceId);
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

    public SearchResult<IReadOnlyDictionary<string, object?>> Search(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller)
    {
        var store = config.SearchEntity.Store;
        return _handlers.TryGetValue(store, out var handler)
            ? handler.Search(config, request, caller)
            : throw new InvalidOperationException($"Nessun handler di ricerca registrato per lo store '{store}' (entità '{config.SearchEntity.Name}').");
    }
}
