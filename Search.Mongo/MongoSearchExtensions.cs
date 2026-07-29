using MongoDB.Driver;
using WeByte.Search.Core;
using WeByte.Search.Core.Metadata;
using WeByte.Search.Core.Validation;

namespace WeByte.Search.Mongo;

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
        string? atlasIndex)
    {
        var sanitized = new SearchRequestSanitizer(map).Sanitize(request);
        new SearchRequestValidator(map).Validate(sanitized);

        var executor = new MongoSearchExecutor<TDocument>(map, atlasIndex ?? "default");
        return executor.Execute(collection, sanitized);
    }
}
