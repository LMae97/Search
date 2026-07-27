using MongoDB.Driver;
using Search.Core;
using Search.Core.Metadata;
using Search.Core.Validation;

namespace Search.Mongo;

/// <summary>
/// Ingresso "senza cerimonie" per chi vuole solo cercare su una collection: mappa + richiesta → risultati, in
/// una riga. Applica la stessa pipeline del facade (potatura → validazione → espansione free-text → esecuzione)
/// ma senza DI, senza handler, senza permessi/tenant (che restano il percorso avanzato via <c>ISearchService</c>).
/// <code>
/// var result = collection.Search(request, map);                     // free-text = OR di contains
/// var result = collection.Search(request, map, FreeTextSearch.Atlas("products_search"));
/// </code>
/// </summary>
public static class MongoSearchExtensions
{
    public static SearchResult<IReadOnlyDictionary<string, object?>> Search<TDocument>(
        this IMongoCollection<TDocument> collection,
        SearchRequest request,
        IEntitySearchMap map,
        FreeTextSearch? freeText = null)
    {
        var strategy = freeText ?? FreeTextSearch.OrContains;

        var sanitized = new SearchRequestSanitizer(map).Sanitize(request);
        new SearchRequestValidator(map).Validate(sanitized);
        var prepared = SearchTextExpansion.Apply(strategy, map, sanitized);

        var executor = new MongoSearchExecutor<TDocument>(map, strategy.AtlasIndex ?? "default");
        return executor.Execute(collection, prepared);
    }
}
