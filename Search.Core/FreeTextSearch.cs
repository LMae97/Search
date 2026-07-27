namespace Search.Core;

/// <summary>Come un'entità interpreta la ricerca full-text libera (<c>SearchRequest.Search</c>).</summary>
public enum SearchTextMode
{
    /// <summary>
    /// OR di <c>contains</c> sui campi <c>IsSearchable</c>. Universale: si espande nell'albero di filtri e
    /// riusa i translator di ogni store (regex Mongo, ILIKE SQL, LINQ). Nessun codice per-store, nessun indice.
    /// </summary>
    OrContains,

    /// <summary>
    /// MongoDB Atlas Search (stage <c>$search</c>). Full-text con rilevanza e autocomplete; richiede un indice
    /// Atlas sui campi searchable. Solo per lo store Mongo.
    /// </summary>
    Atlas
}

/// <summary>
/// Strategia di ricerca full-text di un'entità. È metadato di contratto (accanto a proiezione/sort di default),
/// non una scelta del client: lo stesso testo <c>Search</c> diventa un OR-di-contains su un'entità e un
/// <c>$search</c> Atlas su un'altra, senza che il FE sappia nulla.
/// </summary>
/// <param name="Mode">Come tradurre il free-text.</param>
/// <param name="AtlasIndex">Nome dell'indice Atlas (solo per <see cref="SearchTextMode.Atlas"/>).</param>
public sealed record FreeTextSearch(SearchTextMode Mode, string? AtlasIndex = null)
{
    /// <summary>Default universale: OR di contains sui campi searchable.</summary>
    public static readonly FreeTextSearch OrContains = new(SearchTextMode.OrContains);

    /// <summary>Atlas Search sull'indice indicato ("default" per convenzione).</summary>
    public static FreeTextSearch Atlas(string index = "default") => new(SearchTextMode.Atlas, index);
}
