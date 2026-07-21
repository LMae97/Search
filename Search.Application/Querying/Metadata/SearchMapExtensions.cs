namespace Search.Application.Querying.Metadata;

/// <summary>
/// Helper condivisi sulla mappa dei campi, per non duplicare due regole trasversali nei tre store:
/// la <b>proiezione di default</b> e il <b>campo di ordinamento di default</b> (paginazione deterministica).
/// </summary>

public static class SearchMapExtensions
{
    /// <summary>Nomi dei campi visibili di default, usati quando la proiezione richiesta è vuota.</summary>
    public static IReadOnlyList<string> DefaultProjection(this IEntitySearchMap map) =>
        map.Fields.Values.Where(field => field.VisibleByDefault).Select(field => field.Name).ToList();

    /// <summary>
    /// Campo su cui ordinare quando la richiesta non specifica un sort, per una paginazione deterministica:
    /// prima <c>id</c>, poi <c>createdAt</c>, altrimenti <c>null</c> (nessun default possibile per l'entità).
    /// Regola unica, applicata identica dai tre store.
    /// </summary>
    public static FieldDescriptor? DefaultSortField(this IEntitySearchMap map) =>
        map.TryGetField("id", out var id) ? id
        : map.TryGetField("createdAt", out var createdAt) ? createdAt
        : null;
}
