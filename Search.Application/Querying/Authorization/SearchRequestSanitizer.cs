using Search.Application.Querying.Filters;
using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Authorization;

/// <summary>
/// Adatta una richiesta alla mappa effettiva dell'utente <b>rimuovendo</b> (non rifiutando) i
/// riferimenti a campi non presenti: filtro, ordinamento e proiezione vengono "potati".
/// <para>
/// Attenzione alla semantica del filtro: togliere una clausola da un AND <b>allarga</b> i
/// risultati, da un OR li <b>restringe</b>; un NOT che perde il figlio sparisce; un AND/OR
/// che resta senza figli sparisce (⇒ matcha tutto). Nessun dato del campo viene rivelato.
/// </para>
/// La validazione "dura" (arità, operatore incompatibile) resta al <c>SearchRequestValidator</c>,
/// che gira dopo questa potatura sui soli campi rimasti.
/// </summary>
public sealed class SearchRequestSanitizer
{
    private readonly IEntitySearchMap _map;

    public SearchRequestSanitizer(IEntitySearchMap effectiveMap) => _map = effectiveMap;

    public SearchRequest Sanitize(SearchRequest request)
    {
        var projection = request.Projection.Where(IsKnown).ToList();
        if (projection.Count == 0)
        {
            // Proiezione vuota (o interamente potata) ⇒ i campi visibili di default.
            projection = _map.Fields.Values
                .Where(field => field.VisibleByDefault)
                .Select(field => field.Name)
                .ToList();
        }

        return new SearchRequest
        {
            Filter = Prune(request.Filter),
            Projection = projection,
            Sort = request.Sort.Where(sort => IsKnown(sort.Field)).ToList(),
            Page = request.Page
        };
    }

    private bool IsKnown(string field) => _map.TryGetField(field, out _);

    private FilterNode? Prune(FilterNode? node) => node switch
    {
        null => null,
        ComparisonFilterNode comparison => IsKnown(comparison.Field) ? comparison : null,
        LogicalFilterNode logical => PruneLogical(logical),
        _ => null
    };

    private FilterNode? PruneLogical(LogicalFilterNode node)
    {
        var kept = node.Children.Select(Prune).OfType<FilterNode>().ToList();

        if (node.Operator == LogicalOperator.Not)
            return kept.Count == 1 ? LogicalFilterNode.Not(kept[0]) : null;

        return kept.Count switch
        {
            0 => null,
            1 => kept[0], // AND/OR con un solo figlio ⇒ il figlio stesso
            _ => node.Operator == LogicalOperator.And
                ? LogicalFilterNode.And(kept.ToArray())
                : LogicalFilterNode.Or(kept.ToArray())
        };
    }
}
