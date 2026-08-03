using MongoDB.Bson;
using MongoDB.Driver;
using WeByte.Search.Core;
using WeByte.Search.Core.Metadata;

namespace WeByte.Search.Mongo;

/// <summary>
/// Esegue una <see cref="SearchRequest"/> su una collezione MongoDB — il gemello documentale di
/// <c>LinqSearchExecutor</c>. Filtro, ordinamento, proiezione e paginazione diventano una
/// <c>find</c> Mongo. La proiezione dinamica qui è banale: un <b>projection document</b> con i soli
/// path richiesti (niente over-fetch, niente tipi statici). Ritorna lo stesso
/// <see cref="SearchResult{T}"/> di dizionari del lato relazionale.
/// </summary>
public sealed class MongoSearchExecutor<TDocument>
{
    /// <summary>
    /// Nome dell'indice Atlas Search usato dallo stage <c>$search</c>. "default" è la convenzione di Atlas;
    /// in futuro andrà reso configurabile per-entità (oggi è uno solo per lo slice di valutazione).
    /// </summary>
    //private const string DefaultAtlasIndex = "default";

    private readonly IEntitySearchMap _map;
    private readonly MongoFilterTranslator<TDocument> _filterTranslator;
    private readonly string? _atlasIndex;
    private readonly string? _unwindPath;

    public MongoSearchExecutor(IEntitySearchMap map, string? atlasIndex = null, string? unwindPath = null)
    {
        _map = map;
        _filterTranslator = new MongoFilterTranslator<TDocument>(map);
        _atlasIndex = atlasIndex;
        _unwindPath = unwindPath;
    }

    public SearchResult<IReadOnlyDictionary<string, object?>> Execute(IMongoCollection<TDocument> collection, SearchRequest request)
        => Execute(collection, BuildPlan(request), request.Page);

    /// <summary>
    /// Solo il conteggio (nessuna proiezione/paginazione eseguita): per chi ha bisogno di sapere quante
    /// righe risulterebbero senza pagare il costo dei dati — es. una decisione "l'export è troppo grande?"
    /// PRIMA di scriverlo. Stessa logica di conteggio usata internamente da <see cref="Execute(IMongoCollection{TDocument}, MongoQueryPlan, PageRequest)"/>,
    /// qui isolata perché richiamabile da sola.
    /// </summary>
    /// <param name="upTo">
    /// Limita quante righe il conteggio è disposto a leggere: con un filtro poco selettivo, contare tutto
    /// può costare quanto la query dati. Il risultato è il conteggio vero se resta sotto la soglia,
    /// altrimenti la soglia stessa — non più il totale esatto, ma basta per un confronto.
    /// </param>
    public long Count(IMongoCollection<TDocument> collection, SearchRequest request, long? upTo = null)
        => Count(collection, BuildPlan(request), upTo);

    /// <summary>Come <see cref="Count(IMongoCollection{TDocument}, SearchRequest, long?)"/>, su un piano già costruito.</summary>
    public long Count(IMongoCollection<TDocument> collection, MongoQueryPlan plan, long? upTo = null)
    {
        // Senza $search/$unwind, CountDocuments (find puro) è più economico di un'aggregation; il driver
        // stesso si ferma a Limit documenti invece di scandire tutta la collezione.
        if (plan.SearchStage is null && plan.UnwindStage is null)
            return collection.CountDocuments(plan.Filter, new CountOptions { Limit = upTo });

        var pipeline = BuildPrelude(plan);
        if (upTo is { } limit)
            pipeline.Add(new BsonDocument("$limit", limit));
        pipeline.Add(new BsonDocument("$count", "total"));

        var countDoc = collection
            .Aggregate<BsonDocument>(PipelineDefinition<TDocument, BsonDocument>.Create(pipeline))
            .FirstOrDefault();

        return countDoc is null ? 0 : countDoc["total"].ToInt64();
    }

    /// <summary>
    /// Esegue un piano già costruito (utile a chi vuole prima ispezionarlo/loggarlo). Sceglie il percorso in
    /// base al piano: se c'è free-text (<see cref="MongoQueryPlan.SearchStage"/>) o un unwind
    /// (<see cref="MongoQueryPlan.UnwindStage"/>) serve la <b>aggregation</b> (entrambi richiedono stage che
    /// una <c>find</c> non può esprimere); altrimenti la <c>find</c> classica, più economica.
    /// <para>
    /// Solo dati: nessun conteggio. Il totale è sempre <see cref="Count(IMongoCollection{TDocument}, SearchRequest, long?)"/>,
    /// chiamato a parte — pagarlo anche qui vorrebbe dire farlo due volte per ogni ricerca.
    /// </para>
    /// </summary>
    public SearchResult<IReadOnlyDictionary<string, object?>> Execute(
        IMongoCollection<TDocument> collection, MongoQueryPlan plan, PageRequest page)
        => plan.SearchStage is null && plan.UnwindStage is null
            ? ExecuteFind(collection, plan, page)
            : ExecuteAggregate(collection, plan, page);

    // Percorso classico: filtro puntuale, nessun free-text.
    private SearchResult<IReadOnlyDictionary<string, object?>> ExecuteFind(
        IMongoCollection<TDocument> collection, MongoQueryPlan plan, PageRequest page)
    {
        var find = collection.Find(plan.Filter);
        if (plan.Sort is not null)
            find = find.Sort(plan.Sort);

        var documents = find
            .Skip(plan.Skip)
            .Limit(plan.Limit)
            .Project<BsonDocument>(plan.Projection)
            .ToList();

        return Build(documents, plan, page);
    }

    // Percorso aggregation: $search (se free-text, obbligatoriamente primo) → $unwind (se l'entità esplode una
    // collezione annidata) → $match (filtro, valutato DOPO l'unwind: sia i campi del documento padre sia quelli
    // della collezione esplosa, in un unico stage — niente split pre/post-unwind) → ordinamento → paginazione →
    // proiezione.
    private SearchResult<IReadOnlyDictionary<string, object?>> ExecuteAggregate(
        IMongoCollection<TDocument> collection, MongoQueryPlan plan, PageRequest page)
    {
        var dataPipeline = BuildPrelude(plan);
        if (plan.Sort is not null)
            dataPipeline.Add(new BsonDocument("$sort", plan.Sort)); // sort esplicito; se assente vince la rilevanza Atlas
        dataPipeline.Add(new BsonDocument("$skip", plan.Skip));
        dataPipeline.Add(new BsonDocument("$limit", plan.Limit));
        dataPipeline.Add(new BsonDocument("$project", plan.Projection));

        var documents = collection
            .Aggregate<BsonDocument>(PipelineDefinition<TDocument, BsonDocument>.Create(dataPipeline))
            .ToList();

        return Build(documents, plan, page);
    }

    // Stage comuni a conteggio e dati nel percorso aggregation: $search → $unwind → $match. Da qui in poi
    // divergono (il conteggio chiude con $count, i dati con sort/skip/limit/project).
    private static List<BsonDocument> BuildPrelude(MongoQueryPlan plan)
    {
        var prelude = new List<BsonDocument>();
        if (plan.SearchStage is not null)
            prelude.Add(plan.SearchStage);
        if (plan.UnwindStage is not null)
            prelude.Add(plan.UnwindStage);
        if (plan.Filter.ElementCount > 0)
            prelude.Add(new BsonDocument("$match", plan.Filter));

        return prelude;
    }

    private static SearchResult<IReadOnlyDictionary<string, object?>> Build(
        List<BsonDocument> documents, MongoQueryPlan plan, PageRequest page)
    {
        var items = documents
            .Select(doc => (IReadOnlyDictionary<string, object?>)MapRecord(doc, plan.Fields))
            .ToList();

        return new SearchResult<IReadOnlyDictionary<string, object?>>(items);
    }

    /// <summary>Costruisce la query Mongo senza eseguirla (per ispezione/test).</summary>
    public MongoQueryPlan BuildPlan(SearchRequest request)
    {
        var filter = request.Filter is null
            ? new BsonDocument()
            : _filterTranslator.BuildFilterDocument(request.Filter);

        var searchStage = BuildSearchStage(request.FullTextSearch);
        var unwindStage = _unwindPath is null ? null : new BsonDocument("$unwind", "$" + _unwindPath);
        var (projection, fields) = BuildProjection(request.Projection);
        // Con free-text e senza sort esplicito lasciamo ordinare per rilevanza (nessuno $sort); altrimenti
        // vale il default deterministico (id/createdAt) come per gli altri store.
        var sort = BuildSort(request.Sort, relevanceDefault: searchStage is not null);

        return new MongoQueryPlan(filter, projection, sort, request.Page.Skip, request.Page.Size, fields, searchStage, unwindStage);
    }

    // Free-text → stage $search Atlas: un compound/should di "autocomplete", uno per ogni campo IsSearchable,
    // con minimumShouldMatch=1 (basta che il testo matchi UN campo). Null se non c'è testo o nessun campo è
    // searchable → l'executor ricade sulla find. È QUI che il concetto store-agnostico "Search" diventa Atlas:
    // gli altri store lo tradurranno a modo loro senza toccare questo codice.
    private BsonDocument? BuildSearchStage(string? searchText)
    {
        // Nessun indice Atlas configurato per questa entità (MongoAtlasIndex null) → niente $search, anche
        // se c'è testo e campi IsSearchable: senza indice la query non sarebbe eseguibile su Atlas.
        if (string.IsNullOrWhiteSpace(searchText) || _atlasIndex is null)
            return null;

        var paths = _map.Fields.Values
            .Where(f => f.IsSearchable && f.StoragePath is not null)
            .Select(f => f.StoragePath!)
            .ToList();

        if (paths.Count == 0)
            return null;

        var query = NormalizeQuery(searchText);
        var multiToken = query.Contains(' ');

        var should = new BsonArray(paths.Select(path =>
        {
            var autocomplete = new BsonDocument { { "path", path }, { "query", query } };
            if (multiToken)
                autocomplete.Add("tokenOrder", "sequential"); // i token nell'ordine dato (come il vecchio progetto)
            return new BsonDocument("autocomplete", autocomplete);
        }));

        var compound = new BsonDocument("should", should).Add("minimumShouldMatch", 1);
        return new BsonDocument("$search",
            new BsonDocument { { "index", _atlasIndex }, { "compound", compound } });
    }

    // Trim + collapse degli spazi interni + clip di sicurezza sulla lunghezza. Punto di partenza: la
    // normalizzazione fine (suffissi societari, caratteri speciali) del vecchio progetto si aggiunge qui.
    private static string NormalizeQuery(string raw)
    {
        var collapsed = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        const int maxLength = 100;
        return collapsed.Length > maxLength ? collapsed[..maxLength] : collapsed;
    }

    private (BsonDocument Projection, IReadOnlyList<(string Name, string Path)> Fields) BuildProjection(IReadOnlyList<string> projection)
    {
        var names = projection.Count == 0 ? [] : projection;

        var document = new BsonDocument();
        var fields = new List<(string Name, string Path)>();

        foreach (var name in names)
        {
            if (!_map.TryGetField(name, out var field))
                throw new InvalidOperationException($"Campo '{name}' non mappato.");

            var path = field.StoragePath
                ?? throw new InvalidOperationException($"Il campo '{name}' non ha uno StoragePath (richiesto per Mongo).");

            if (field.SecondaryStoragePath is not null)
            {
                // Proiezione composta { value, <SecondaryResponseKey> }: stesso principio del
                // json_build_object lato SQL — un solo campo pubblico che combina due path del documento.
                // L'alias diventa il "path" da rileggere in MapRecord: non punta più dentro il documento
                // originale, ma alla sotto-struttura appena costruita da questo stage.
                document[name] = new BsonDocument
                {
                    { "value", "$" + path },
                    { field.SecondaryResponseKey!, "$" + field.SecondaryStoragePath }
                };
                fields.Add((name, name));
                continue;
            }

            document[path] = 1;
            fields.Add((name, path));
        }

        // Escludi _id se nessun campo lo richiede esplicitamente (Mongo lo includerebbe di default).
        if (!fields.Any(f => f.Path == "_id"))
            document["_id"] = 0;

        return (document, fields);
    }

    private BsonDocument? BuildSort(IReadOnlyList<SortField> sorts, bool relevanceDefault = false)
    {
        if (sorts.Count == 0)
        {
            // Con free-text senza sort esplicito: nessuno $sort, ordina la rilevanza di Atlas.
            if (relevanceDefault)
                return null;

            // Nessun sort esplicito → default deterministico (id/createdAt) se l'entità ce l'ha, come gli altri store.
            var def = _map.DefaultSortField();
            return def is null ? null : new BsonDocument { [PathOf(def.Name)] = 1 };
        }

        var document = new BsonDocument();
        foreach (var sort in sorts)
            document[PathOf(sort.Field)] = sort.Direction == WeByte.Search.Core.SortDirection.Ascending ? 1 : -1;
        return document;
    }

    private string PathOf(string fieldName)
    {
        if (!_map.TryGetField(fieldName, out var field))
            throw new InvalidOperationException($"Campo '{fieldName}' non mappato.");
        return field.StoragePath
            ?? throw new InvalidOperationException($"Il campo '{fieldName}' non ha uno StoragePath (richiesto per Mongo).");
    }

    private static Dictionary<string, object?> MapRecord(BsonDocument document, IReadOnlyList<(string Name, string Path)> fields)
    {
        var record = new Dictionary<string, object?>(fields.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, path) in fields)
            record[name] = ToClr(GetByPath(document, path));
        return record;
    }

    private static BsonValue? GetByPath(BsonDocument document, string path)
    {
        BsonValue current = document;
        foreach (var segment in path.Split('.'))
        {
            if (current is BsonDocument doc && doc.TryGetValue(segment, out var next))
                current = next;
            else
                return null;
        }
        return current;
    }

    private static object? ToClr(BsonValue? value)
    {
        if (value is null || value.IsBsonNull)
            return null;

        return value.BsonType switch
        {
            BsonType.ObjectId => value.AsObjectId.ToString(),   // es. "6754248f43ad677b83600fad" (non lo struct {timestamp,...})
            BsonType.String => value.AsString,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Decimal128 => (decimal)value.AsDecimal128,
            BsonType.Double => value.AsDouble,
            BsonType.Boolean => value.AsBoolean,
            BsonType.DateTime => value.ToUniversalTime(),
            BsonType.Array => value.AsBsonArray.Select(ToClr).ToList(),
            // Proiezione composta (vedi BuildProjection, campi con SecondaryStoragePath): un vero oggetto
            // annidato, non un valore scalare — va reso come Dictionary, non lasciato come BsonDocument
            // (che il serializzatore JSON a valle non saprebbe scrivere correttamente, com'era già emerso
            // per l'equivalente SQL/json_build_object).
            BsonType.Document => value.AsBsonDocument.Elements.ToDictionary(e => e.Name, e => ToClr(e.Value)),
            _ => BsonTypeMapper.MapToDotNetValue(value)
        };
    }
}

/// <summary>Query Mongo costruita dal motore (per esecuzione o ispezione).</summary>
/// <param name="SearchStage">Stage <c>$search</c> Atlas quando c'è free-text; null = ricerca via <c>find</c>.</param>
/// <param name="UnwindStage">Stage <c>$unwind</c> quando l'entità esplode una collezione annidata; null = nessuno.</param>
public sealed record MongoQueryPlan(
    BsonDocument Filter,
    BsonDocument Projection,
    BsonDocument? Sort,
    int Skip,
    int Limit,
    IReadOnlyList<(string Name, string Path)> Fields,
    BsonDocument? SearchStage = null,
    BsonDocument? UnwindStage = null);
