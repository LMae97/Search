using WeByte.Search.Application.Config;
using WeByte.Search.Application.Querying;
using WeByte.Search.Application.Querying.Authorization;
using WeByte.Search.Application.Querying.Dynamic;
using WeByte.Search.Application.Search;
using WeByte.Search.Core;
using WeByte.Search.Core.Metadata;
using WeByte.Search.Export;

namespace WeByte.Search.Application.Export;

/// <summary>
/// Colla fra il motore di ricerca e uno scrittore tabellare: esegue <b>un'unica ricerca</b> e riversa il
/// risultato nello stream.
/// <para>
/// Requisito di progetto: mai paginare in più round-trip separati. Una tabella che cambia fra una query e
/// l'altra rende una paginazione a offset inconsistente — righe inserite/cancellate nel frattempo spostano
/// cosa si trova "all'offset N", causando duplicati o righe saltate fra un batch e il successivo. Una
/// singola query è una lettura sola, coerente per costruzione: per questo <see cref="Decide"/> esiste — va
/// chiamato prima per sapere se l'export rientra in una query sola, senza doverla eseguire per scoprirlo.
/// </para>
/// <para>
/// È <b>indipendente dal formato</b>: riceve una <see cref="ITabularWriterFactory"/>, quindi questo progetto
/// non referenzia né CSV né ClosedXML.
/// </para>
/// <para>
/// Le due decisioni che prende sono di policy, non di formato: <b>quali colonne finiscono nel file</b> (solo
/// le visibili: i campi nascosti stanno in proiezione per servire filtri e ordinamenti, non per essere
/// mostrati) e <b>da dove arrivano i dati</b>.
/// </para>
/// </summary>
public sealed class SearchExporter
{
    private readonly DbBackedSearchMapProvider _maps;
    private readonly ISearchService _search;

    public SearchExporter(DbBackedSearchMapProvider maps, ISearchService search)
    {
        _maps = maps;
        _search = search;
    }

    /// <summary>
    /// Decide se l'export rientra nella soglia sincrona, PRIMA di scrivere nulla.
    /// <list type="bullet">
    /// <item>Il numero di colonne è <b>esatto</b> (stessa risoluzione di <see cref="Write"/>, vedi
    /// <see cref="ResolveColumns"/>), da cui la soglia <c>maxSyncRows = MaxSyncCells / colonne</c>.</item>
    /// <item>Il conteggio delle righe è <see cref="ISearchService.Count"/> con un tetto di
    /// <c>maxSyncRows + SafetyMargin</c>: non serve sapere QUANTO è grande un export che sfora, basta
    /// sapere CHE sfora — un conteggio senza limite su un filtro poco selettivo costerebbe quanto la query
    /// dati che si sta cercando di evitare.</item>
    /// </list>
    /// <para>
    /// Chiamare prima di <see cref="Write"/> quando la richiesta arriva da un contesto con un tempo di
    /// risposta limitato (una richiesta HTTP): se torna <see cref="ExportDecision.TooLargeForSync"/>, chi
    /// chiama decide come deferire (coda, funzione esterna, ecc.) — questa classe non lo sa fare.
    /// </para>
    /// </summary>
    public ExportDecision Decide(
        ISearchableEntityConfig config,
        SearchRequest request,
        SearchCaller caller,
        ExportSizeLimit? limit = null)
    {
        var map = _maps.GetEffectiveMap(config.SearchEntity, caller);
        var columnCount = ResolveColumns(map, config, request).Count;

        var effectiveLimit = limit ?? ExportSizeLimit.Default;
        var maxSyncRows = effectiveLimit.MaxSyncRows(columnCount);

        // Il conteggio si ferma a maxSyncRows + margine: se il vero totale è più alto, torna comunque solo
        // quel tetto (non il totale reale) — sufficiente per la decisione, molto più economico di un COUNT
        // senza limite quando il filtro è poco selettivo.
        var rowCount = _search.Count(config, request, caller, upTo: maxSyncRows + 10);

        return rowCount > maxSyncRows
            ? new ExportDecision.TooLargeForSync(rowCount, maxSyncRows)
            : new ExportDecision.Sync();
    }

    /// <summary>
    /// Esegue <b>un'unica ricerca</b> e scrive il file in <paramref name="destination"/> (che resta aperto).
    /// <para>
    /// <b>La paginazione di <paramref name="request"/> viene ignorata</b>: la richiesta effettiva chiede
    /// sempre pagina 1, di dimensione <see cref="ExportPagingOptions.MaxRows"/> (o senza limite se assente).
    /// </para>
    /// </summary>
    public void Write(
        Stream destination,
        ITabularWriterFactory writerFactory,
        ISearchableEntityConfig config,
        SearchRequest request,
        SearchCaller caller,
        ExportPagingOptions? paging = null)
    {
        ArgumentNullException.ThrowIfNull(writerFactory);

        var map = _maps.GetEffectiveMap(config.SearchEntity, caller);
        var columns = ResolveColumns(map, config, request);

        // Nessun limite richiesto ⇒ "tutte le righe che il filtro seleziona": non esiste un valore di
        // PageRequest.Size che significhi "senza limite", quindi si usa il più grande possibile. Numero
        // enorme passato a LIMIT/Skip+Limit è innocuo per SQL/Mongo (nessuna differenza pratica da "nessun
        // limite" per una tabella reale) — ed è anche il tetto passato al validator, che deve accettarlo.
        var maxRows = (paging ?? ExportPagingOptions.Default).MaxRows ?? int.MaxValue;

        var result = _search.Search(config, WithPage(request, 1, maxRows), caller, maxPageSize: maxRows);
        var rows = StripHiddenFields(result.Body, map);

        // Per l'xlsx il Dispose non è solo un flush: è il momento in cui il workbook viene serializzato.
        using var writer = writerFactory.Create(destination, columns);

        writer.WriteHeader();
        writer.WriteRows(rows);
    }

    // Stessa risoluzione di SearchHandlerBase (ProjectionResolver.Resolve), più la stessa potatura
    // che applica il sanitizer (un campo richiesto ma non presente nella mappa effettiva — permessi/tenant
    // — va scartato, non genera colonna) e il filtro sui campi non esportabili. Prima di questa condivisione,
    // l'export ricalcolava un'euristica propria (solo `request.Projection.Count`), che poteva divergere dal
    // conteggio vero della ricerca.
    private static IReadOnlyList<FieldDescriptor> ResolveColumns(
        IEntitySearchMap map,
        ISearchableEntityConfig config,
        SearchRequest request)
    {
        var resolved = ProjectionResolver.Resolve(map, config, request);
        var known = new SearchRequestSanitizer(map).Sanitize(new SearchRequest { Projection = resolved }).Projection;

        return [.. known.Select(name => map.Fields[name]).Where(IsExportable)];
    }

    // Un campo NON finisce nel file quando:
    //  - è nascosto (IsHidden): in proiezione solo per servire filtri/ordinamenti/la chiave di riga della
    //    griglia (es. "id"), mai da mostrare — corretto per la griglia, non per un file;
    //  - è FieldKind.Custom: un pulsante o un'immagine della UI, non un dato — in un file non ha senso.
    // La griglia (endpoint /search) non applica questo filtro: entrambi i casi restano nella risposta,
    // perché lì servono davvero (la griglia mostra il bottone, usa "id" come chiave di riga).
    private static bool IsExportable(FieldDescriptor field) => !field.IsHidden && field.Kind != FieldKind.Custom;

    // Post-processing esplicito sui risultati della ricerca: la ricerca torna anche i campi non esportabili
    // (vedi IsExportable) — corretti per la griglia, non per un file. Qui si tolgono per nome, non
    // ricalcolando le colonne: usa la stessa mappa già risolta, quindi non serve una seconda query né una
    // seconda decisione su cosa sia esportabile.
    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> StripHiddenFields(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        IEntitySearchMap map)
    {
        return [.. rows.Select(row => (IReadOnlyDictionary<string, object?>)row
            .Where(cell => !map.TryGetField(cell.Key, out var field) || IsExportable(field))
            .ToDictionary(cell => cell.Key, cell => cell.Value))];
    }

    // SearchRequest è una classe con proprietà init-only: nessun `with`, si ricostruisce come fa
    // SearchHandlerBase. La paginazione è l'unica cosa che cambia fra un batch e il successivo.
    private static SearchRequest WithPage(SearchRequest request, int number, int size) => new()
    {
        FullTextSearch = request.FullTextSearch,
        Filter = request.Filter,
        Projection = request.Projection,
        Sort = request.Sort,
        Page = new PageRequest(number, size)
    };
}
