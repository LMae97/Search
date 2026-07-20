using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Metadata;
using Search.Application.Querying.Validation;

namespace Search.Application.Querying;

/// <summary>
/// Ingresso unico e store-agnostic per le ricerche: data la chiave-entità delega alla strategia corretta,
/// nascondendo al chiamante se sotto giri SQL grezzo, LINQ/EF o Mongo (Facade).
/// </summary>
public interface ISearchService
{
    SearchResult<IReadOnlyDictionary<string, object?>> Search(string entity, SearchRequest request, SearchCaller caller);
}

/// <summary>
/// Strategia di ricerca per una singola entità: sa eseguire sé stessa contro il proprio store. Il facade
/// ne tiene una per chiave e smista. Aggiungere un'entità = registrare un nuovo handler (Open/Closed principle).
/// </summary>
public interface ISearchHandler
{
    string EntityName { get; }
    SearchResult<IReadOnlyDictionary<string, object?>> Search(SearchRequest request, SearchCaller caller);
}

/// <summary>
/// Base che centralizza la pipeline comune a tutti gli store: mappa effettiva (permessi/tenant) → potatura →
/// validazione. L'unico pezzo store-specifico è <see cref="Execute"/>, implementato dagli handler concreti.
/// </summary>
public abstract class SearchHandlerBase(string entityName, DbBackedSearchMapProvider maps) : ISearchHandler
{
    public string EntityName => entityName;

    public SearchResult<IReadOnlyDictionary<string, object?>> Search(SearchRequest request, SearchCaller caller)
    {
        var map = maps.GetEffectiveMap(EntityName, caller);
        var sanitized = new SearchRequestSanitizer(map).Sanitize(request);
        new SearchRequestValidator(map).Validate(sanitized);
        return Execute(map, sanitized);
    }

    /// <summary>Esegue la richiesta già sanificata/validata contro lo store concreto.</summary>
    protected abstract SearchResult<IReadOnlyDictionary<string, object?>> Execute(IEntitySearchMap map, SearchRequest request);
}

/// <summary>Facade: risolve l'handler per nome-entità e delega.</summary>
public sealed class SearchService(IEnumerable<ISearchHandler> handlers) : ISearchService
{
    private readonly IReadOnlyDictionary<string, ISearchHandler> _handlers =
        handlers.ToDictionary(handler => handler.EntityName, StringComparer.OrdinalIgnoreCase);

    public SearchResult<IReadOnlyDictionary<string, object?>> Search(string entity, SearchRequest request, SearchCaller caller)
        => _handlers.TryGetValue(entity, out var handler)
            ? handler.Search(request, caller)
            : throw new InvalidOperationException($"Nessuna ricerca registrata per l'entità '{entity}'.");
}
