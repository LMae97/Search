using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Metadata;
using Search.Application.Querying.Validation;

namespace Search.Application.Querying;

/// <summary>
/// Ingresso unico e store-agnostic per le ricerche: data la chiave-entità delega alla strategia dello store
/// giusto, nascondendo al chiamante se sotto giri SQL grezzo, LINQ/EF o Mongo (Facade).
/// </summary>
public interface ISearchService
{
    SearchResult<IReadOnlyDictionary<string, object?>> Search(string entity, SearchRequest request, SearchCaller caller);
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
    SearchResult<IReadOnlyDictionary<string, object?>> Search(string entityName, SearchRequest request, SearchCaller caller);
}

/// <summary>
/// Base che centralizza la pipeline comune a tutti gli store: mappa effettiva (permessi/tenant) → potatura →
/// validazione. L'unico pezzo store-specifico è <see cref="Execute"/>, implementato dagli handler concreti.
/// </summary>
public abstract class SearchHandlerBase(DbBackedSearchMapProvider maps) : ISearchHandler
{
    public abstract StoreKind Store { get; }

    public SearchResult<IReadOnlyDictionary<string, object?>> Search(string entityName, SearchRequest request, SearchCaller caller)
    {
        var map = maps.GetEffectiveMap(entityName, caller);
        var sanitized = new SearchRequestSanitizer(map).Sanitize(request);
        new SearchRequestValidator(map).Validate(sanitized);
        return Execute(entityName, map, sanitized);
    }

    /// <summary>Esegue la richiesta già sanificata/validata contro lo store concreto.</summary>
    protected abstract SearchResult<IReadOnlyDictionary<string, object?>> Execute(string entityName, IEntitySearchMap map, SearchRequest request);
}

/// <summary>
/// Facade: dal <see cref="SearchEntityRegistry"/> ricava lo store dell'entità e delega all'handler di quello store.
/// </summary>
public sealed class SearchService(IEnumerable<ISearchHandler> handlers, SearchEntityRegistry entities) : ISearchService
{
    private readonly IReadOnlyDictionary<StoreKind, ISearchHandler> _handlers =
        handlers.ToDictionary(handler => handler.Store);

    public SearchResult<IReadOnlyDictionary<string, object?>> Search(string entity, SearchRequest request, SearchCaller caller)
    {
        var store = entities.Get(entity).Store;
        return _handlers.TryGetValue(store, out var handler)
            ? handler.Search(entity, request, caller)
            : throw new InvalidOperationException($"Nessun handler di ricerca registrato per lo store '{store}' (entità '{entity}').");
    }
}
