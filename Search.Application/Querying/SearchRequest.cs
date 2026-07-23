using Search.Application.Querying.Filters;

namespace Search.Application.Querying;

/** Esempio di richiesta:
 * {
 *   "filter": {
 *     "and": [
 *       { "field": "price", "op": "gte", "value": 10 },
 *       { "or": [
 *         { "field": "tags", "op": "containsAny", "values": ["sale"] },
 *         { "not": { "field": "status", "op": "eq", "value": "Discontinued" } }
 *       ] }
 *     ]
 *   },
 *   "projection": ["name", "price", "status", "tags"]
 * }
 */

/// <summary>
/// Richiesta di ricerca completa: filtro + proiezione + ordinamento + paginazione.
/// È il contratto unico che vale per ogni entità e per entrambi gli store.
/// </summary>
public class SearchRequest
{
    /// <summary>Albero di filtri. Null = nessun filtro (tutti i record).</summary>
    public FilterNode? Filter { get; init; }

    /// <summary>Campi da restituire. Vuoto = tutti i campi mappati.</summary>
    public IReadOnlyList<string> Projection { get; init; } = [];

    public IReadOnlyList<SortField> Sort { get; init; } = [];

    public PageRequest Page { get; init; } = PageRequest.Default;
}

public enum SortDirection
{
    Ascending,
    Descending
}

public sealed record SortField(string Field, SortDirection Direction);

public sealed record PageRequest(int Number, int Size)
{
    public static PageRequest Default { get; } = new(1, 20);

    /// <summary>Record da saltare (paginazione offset). Per Mongo su grandi volumi valuteremo il keyset.</summary>
    public int Skip => (Number - 1) * Size;
}
