using Search.Application.Config;
using Search.Application.Querying.Filters;
using Search.Application.Querying.Metadata;

namespace Search.Application.Querying;

/// <summary>
/// Traduce il free-text (<see cref="SearchRequest.Search"/>) secondo la strategia dell'entità, PRIMA che la
/// richiesta raggiunga lo store:
/// <list type="bullet">
/// <item><b>OrContains</b>: il testo diventa un OR di <c>contains</c> sui campi <c>IsSearchable</c> e si fonde
/// (in AND) col filtro esistente. Riusa i translator di ogni store → funziona ovunque senza codice dedicato.</item>
/// <item><b>Atlas</b>: non tocca nulla, lascia <see cref="SearchRequest.Search"/> intatto → sarà lo store Mongo
/// a costruire lo stage <c>$search</c>.</item>
/// </list>
/// Gira DOPO la validazione (come il tenant scope): i nodi generati puntano a campi già in whitelist, quindi
/// sono fidati e non potabili/falsificabili dal client.
/// </summary>
public static class SearchTextExpansion
{
    public static SearchRequest Apply(FreeTextSearch strategy, IEntitySearchMap map, SearchRequest request)
    {
        // Niente testo, oppure lo gestisce lo store (Atlas): la richiesta passa invariata.
        if (string.IsNullOrWhiteSpace(request.Search) || strategy.Mode == SearchTextMode.Atlas)
            return new SearchRequest
            {
                Search = request.Search,
                Filter = request.Filter,
                Projection = request.Projection,
                Sort = request.Sort,
                Page = request.Page
            };

        var searchableFields = map.Fields.Values
            .Where(field => field.IsSearchable)
            .Select(field => field.Name)
            .ToList();

        // Nessun campo searchable configurato → il free-text non ha dove andare: lo si ignora (niente filtro).
        if (searchableFields.Count == 0)
            return request;

        var contains = searchableFields
            .Select(name => (FilterNode)Filter.Contains(name, request.Search))
            .ToArray();

        // Un solo campo → il contains da solo; più campi → OR. (Un OR con un figlio sarebbe comunque corretto,
        // ma così l'albero resta pulito.)
        var freeText = contains.Length == 1 ? contains[0] : Filter.Or(contains);
        var combined = request.Filter is null ? freeText : Filter.And(request.Filter, freeText);

        return new SearchRequest
        {
            Search = null, // consumato: da qui in poi è un filtro come gli altri
            Filter = combined,
            Projection = request.Projection,
            Sort = request.Sort,
            Page = request.Page
        };
    }
}
