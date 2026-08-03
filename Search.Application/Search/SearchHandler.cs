using WeByte.Search.Application.Config;
using WeByte.Search.Application.Dtos;
using WeByte.Search.Application.Querying;
using WeByte.Search.Application.Querying.Authorization;
using WeByte.Search.Application.Querying.Dynamic;
using WeByte.Search.Core;
using WeByte.Search.Core.Dynamic;
using WeByte.Search.Core.Metadata;
using WeByte.Search.Core.Validation;

namespace WeByte.Search.Application.Search;

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

    SearchResponseDto SearchWithCount(
        ISearchableEntityConfig config,
        SearchRequest request,
        SearchCaller caller,
        long? maxPageSize = 20,
        long? maxTotalCount = 1000);

    SearchResponseDto Search(
        ISearchableEntityConfig config,
        SearchRequest request,
        SearchCaller caller,
        long? maxPageSize = 20);

    long Count(
        ISearchableEntityConfig config, 
        SearchRequest request, 
        SearchCaller caller, 
        long? upTo = null);
}

/// <summary>
/// Base che centralizza la pipeline comune a tutti gli store: mappa effettiva (permessi/tenant) → potatura →
/// validazione. L'unico pezzo store-specifico è <see cref="Execute"/>, implementato dagli handler concreti.
/// </summary>
public abstract class SearchHandlerBase(DbBackedSearchMapProvider maps) : ISearchHandler
{
    /** FLUSSO
     *   SearchHandlerBase.SearchWithCount
     *     ├─ mappa effettiva → sanitize → validate
     *     ├─ ProjectionResolver.Resolve: proiezione vuota → DefaultProjection() della mappa → DefaultProjection() della config
     *     ├─ AdaptSort: sort vuoto → DefaultSort della config, poi tiebreak su IdField se non già presente
     *     └─ Execute(config, map, resolved, spaceId)
     *           └─ FullTextSearch passa invariato: NON è più espanso qui. È l'adapter applicativo (fuori da
     *              questa pipeline, es. ContractBaseFilters) a decidere se tradurlo in un filtro OR-di-contains
     *              o lasciarlo per il full-text nativo dello store (oggi solo Mongo, via MongoAtlasIndex).
     */
    public abstract StoreKind Store { get; }

    public SearchResponseDto SearchWithCount(
        ISearchableEntityConfig config,
        SearchRequest request,
        SearchCaller caller,
        long? maxPageSize = 20,
        long? maxTotalCount = 1000)
    {
        var map = maps.GetEffectiveMap(config.SearchEntity, caller);

        var adapted = new SearchRequest
        {
            FullTextSearch = request.FullTextSearch,
            Filter = request.Filter,
            Projection = ProjectionResolver.Resolve(map, config, request),
            Sort = SortResolver.Resolve(config, request),
            Page = request.Page
        };

        var sanitized = new SearchRequestSanitizer(map).Sanitize(adapted);
        new SearchRequestValidator(map, maxPageSize.HasValue ? (int)maxPageSize.Value : 20).Validate(sanitized);

        var result = Execute(config, map, sanitized, caller.SpaceId);

        // Stesso ordine della proiezione risolta (non l'ordine interno di map.Fields, arbitrario/non garantito).
        var prjFields = sanitized.Projection.Select(name => map.Fields[name]);

        // Eseguo una query count cappata a 1000
        var countRequest = new SearchRequest { FullTextSearch = sanitized.FullTextSearch, Filter = sanitized.Filter };
        var count = ExecuteCount(config, map, countRequest, caller.SpaceId, maxTotalCount);

        return SearchResponseAdapter.ToSearchResponseDto(prjFields, result.Items, count);
    }

    public SearchResponseDto Search(
        ISearchableEntityConfig config,
        SearchRequest request,
        SearchCaller caller,
        long? maxPageSize = 20)
    {
        var map = maps.GetEffectiveMap(config.SearchEntity, caller);

        var adapted = new SearchRequest
        {
            FullTextSearch = request.FullTextSearch,
            Filter = request.Filter,
            Projection = ProjectionResolver.Resolve(map, config, request),
            Sort = SortResolver.Resolve(config, request),
            Page = request.Page
        };

        var sanitized = new SearchRequestSanitizer(map).Sanitize(adapted);
        new SearchRequestValidator(map, maxPageSize.HasValue ? (int)maxPageSize.Value : 20).Validate(sanitized);
        var result = Execute(config, map, sanitized, caller.SpaceId);
        // Stesso ordine della proiezione risolta (non l'ordine interno di map.Fields, arbitrario/non garantito).
        var prjFields = sanitized.Projection.Select(name => map.Fields[name]);

        return SearchResponseAdapter.ToSearchResponseDto(prjFields, result.Items, null);
    }

    /// <summary>
    /// Pipeline gemella di <see cref="SearchWithCount"/>, ridotta al minimo che serve a un conteggio: mappa effettiva
    /// → potatura del <b>solo filtro</b> (proiezione/ordinamento non contano per un conteggio, quindi non si
    /// risolvono affatto) → validazione → <see cref="ExecuteCount"/>. Nessuna query sui dati.
    /// </summary>
    public long Count(
        ISearchableEntityConfig config, 
        SearchRequest request, 
        SearchCaller caller, 
        long? upTo = null)
    {
        var map = maps.GetEffectiveMap(config.SearchEntity, caller);

        var sanitizedFilter = new SearchRequestSanitizer(map).Sanitize(request).Filter;
        var sanitized = new SearchRequest { FullTextSearch = request.FullTextSearch, Filter = sanitizedFilter };

        new SearchRequestValidator(map).Validate(sanitized);

        return ExecuteCount(config, map, sanitized, caller.SpaceId, upTo);
    }

    /// <summary>
    /// Esegue la richiesta già sanificata/validata/espansa contro lo store concreto. Solo dati: il conteggio
    /// è sempre <see cref="ExecuteCount"/>, chiamato a parte — pagarlo qui vorrebbe dire farlo due volte.
    /// </summary>
    protected abstract SearchResult<IReadOnlyDictionary<string, object?>> Execute(
        ISearchableEntityConfig config, 
        IEntitySearchMap map, 
        SearchRequest request, 
        Guid spaceId);

    /// <summary>
    /// Conta i risultati del solo filtro (già sanificato/validato), senza eseguire la query dati.
    /// <paramref name="upTo"/>: vedi <see cref="ISearchService.Count"/>.
    /// </summary>
    protected abstract long ExecuteCount(
        ISearchableEntityConfig config, 
        IEntitySearchMap map, 
        SearchRequest request, 
        Guid spaceId, 
        long? upTo);
}
