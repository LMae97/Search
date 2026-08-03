using WeByte.Search.Application.Config;
using WeByte.Search.Core;
using WeByte.Search.Application.Querying.Authorization;
using WeByte.Search.Core.Dynamic;
using WeByte.Search.Application.Dtos;

namespace WeByte.Search.Application.Search;

/// <summary>
/// Ingresso unico e store-agnostic per le ricerche: data la chiave-entità delega alla strategia dello store
/// giusto, nascondendo al chiamante se sotto giri SQL grezzo, LINQ/EF o Mongo (Facade).
/// </summary>
public interface ISearchService
{
    /// <param name="maxPageSize">
    /// Tetto di <see cref="PageRequest.Size"/> accettato dalla richiesta. Default 200, pensato per una
    /// griglia UI. Un chiamante interno che pagina a batch — l'export — deve passare il proprio tetto di
    /// batch, altrimenti una richiesta legittima da migliaia di righe verrebbe rifiutata da un vincolo
    /// pensato per tutt'altro caso d'uso.
    /// </param>
    /// <param name="maxResultCount">
    /// Tetto di <see cref="SearchResponseDto.ResultCount"/> (il totale mostrato dalla griglia, non le righe
    /// restituite in questa pagina). Un filtro poco selettivo su una tabella enorme rende il conteggio del
    /// totale costoso quanto (o più di) la query dati stessa; oltre questa soglia, mostrare "1000+" invece
    /// del totale esatto è la stessa scelta di ottimizzazione del vecchio sistema (<c>CountLimit</c>).
    /// <c>null</c> = conteggio esatto, senza limite.
    /// </param>
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

    /// <summary>
    /// Solo il conteggio dei risultati, senza eseguire alcuna query sui dati (proiezione/ordinamento/pagina
    /// sono ignorati). Per chi deve decidere qualcosa in base a "quanti risultati ci sono" prima di pagare
    /// il costo di leggerli — es. l'export, che deve sapere se è troppo grande PRIMA di scriverlo.
    /// </summary>
    /// <param name="upTo">
    /// Limita quante righe il conteggio è disposto a leggere: con un filtro poco selettivo su una tabella
    /// enorme, un conteggio senza limite può costare quanto (o più di) la query dati stessa. Il risultato è
    /// il conteggio vero se resta sotto la soglia, altrimenti la soglia stessa — sufficiente per un
    /// confronto, non più garantito come totale esatto. <c>null</c> = nessun limite.
    /// </param>
    long Count(
        ISearchableEntityConfig config, 
        SearchRequest request, 
        SearchCaller caller, 
        long? upTo = null);


}


/// <summary>
/// Facade: dal <see cref="SearchEntityRegistry"/> ricava lo store dell'entità e delega all'handler di quello store.
/// </summary>
public sealed class SearchService(IEnumerable<ISearchHandler> handlers) : ISearchService
{
    private readonly IReadOnlyDictionary<StoreKind, ISearchHandler> _handlers =
        handlers.ToDictionary(handler => handler.Store);

    public SearchResponseDto SearchWithCount(
        ISearchableEntityConfig config, 
        SearchRequest request, 
        SearchCaller caller, 
        long? maxPageSize = 20, 
        long? maxTotalCount = 1000) =>
            Handler(config).SearchWithCount(config, request, caller, maxPageSize, maxTotalCount);

    public SearchResponseDto Search(
        ISearchableEntityConfig config, 
        SearchRequest request, 
        SearchCaller caller, 
        long? maxPageSize = 20) =>
            Handler(config).Search(config, request, caller, maxPageSize);

    public long Count(
        ISearchableEntityConfig config, 
        SearchRequest request, 
        SearchCaller caller, 
        long? upTo = null) =>
            Handler(config).Count(config, request, caller, upTo);

    private ISearchHandler Handler(ISearchableEntityConfig config)
    {
        var store = config.SearchEntity.Store;
        return _handlers.TryGetValue(store, out var handler)
            ? handler
            : throw new InvalidOperationException($"Nessun handler di ricerca registrato per lo store '{store}' (entità '{config.SearchEntity.Name}').");
    }
}
