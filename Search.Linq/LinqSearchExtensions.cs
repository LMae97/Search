using WeByte.Search.Core;
using WeByte.Search.Core.Metadata;
using WeByte.Search.Core.Validation;

namespace WeByte.Search.Linq;

/// <summary>
/// Ingresso "senza cerimonie" per cercare su una sorgente LINQ (IQueryable EF o oggetti in memoria): mappa +
/// richiesta → risultati, in una riga. Stessa pipeline del facade (potatura → validazione → espansione free-text
/// → esecuzione), senza DI né permessi/tenant. Il free-text qui è sempre OR-di-contains (Atlas è solo Mongo).
/// <code>
/// var result = query.Search(request, map);   // query: IQueryable&lt;T&gt; (EF) o IEnumerable&lt;T&gt; in memoria
/// </code>
/// </summary>
public static class LinqSearchExtensions
{
    public static SearchResult<IReadOnlyDictionary<string, object?>> Search<T>(
        this IQueryable<T> source, SearchRequest request, IEntitySearchMap map)
    {
        var sanitized = new SearchRequestSanitizer(map).Sanitize(request);
        new SearchRequestValidator(map).Validate(sanitized);

        return new LinqSearchExecutor<T>(map).Execute(source, sanitized);
    }

    public static SearchResult<IReadOnlyDictionary<string, object?>> Search<T>(
        this IEnumerable<T> source, SearchRequest request, IEntitySearchMap map)
        => source.AsQueryable().Search(request, map);
}
