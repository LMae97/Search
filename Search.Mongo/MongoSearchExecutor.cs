using MongoDB.Bson;
using MongoDB.Driver;
using Search.Core;
using Search.Core.Metadata;

namespace Search.Mongo;

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
    private const string DefaultAtlasIndex = "default";

    private readonly IEntitySearchMap _map;
    private readonly MongoFilterTranslator<TDocument> _filterTranslator;
    private readonly string _atlasIndex;
    private readonly string? _unwindPath;

    public MongoSearchExecutor(IEntitySearchMap map, string atlasIndex = DefaultAtlasIndex, string? unwindPath = null)
    {
        _map = map;
        _filterTranslator = new MongoFilterTranslator<TDocument>(map);
        _atlasIndex = atlasIndex;
        _unwindPath = unwindPath;
    }

    public SearchResult<IReadOnlyDictionary<string, object?>> Execute(IMongoCollection<TDocument> collection, SearchRequest request)
        => Execute(collection, BuildPlan(request), request.Page);

    /// <summary>
    /// Esegue un piano già costruito (utile a chi vuole prima ispezionarlo/loggarlo). Sceglie il percorso in
    /// base al piano: se c'è free-text (<see cref="MongoQueryPlan.SearchStage"/>) o un unwind
    /// (<see cref="MongoQueryPlan.UnwindStage"/>) serve la <b>aggregation</b> (entrambi richiedono stage che
    /// una <c>find</c> non può esprimere); altrimenti la <c>find</c> classica, più economica.
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
        var total = collection.CountDocuments(plan.Filter);

        var find = collection.Find(plan.Filter);
        if (plan.Sort is not null)
            find = find.Sort(plan.Sort);

        var documents = find
            .Skip(plan.Skip)
            .Limit(plan.Limit)
            .Project<BsonDocument>(plan.Projection)
            .ToList();

        return Build(documents, plan, page, total);
    }

    // Percorso aggregation: $search (se free-text, obbligatoriamente primo) → $unwind (se l'entità esplode una
    // collezione annidata) → $match (filtro, valutato DOPO l'unwind: sia i campi del documento padre sia quelli
    // della collezione esplosa, in un unico stage — niente split pre/post-unwind) → ordinamento → paginazione →
    // proiezione. Il conteggio è una pipeline gemella che termina con $count (CountDocuments non vede $search/$unwind).
    private SearchResult<IReadOnlyDictionary<string, object?>> ExecuteAggregate(
        IMongoCollection<TDocument> collection, MongoQueryPlan plan, PageRequest page)
    {
        var prelude = new List<BsonDocument>();
        if (plan.SearchStage is not null)
            prelude.Add(plan.SearchStage);
        if (plan.UnwindStage is not null)
            prelude.Add(plan.UnwindStage);
        if (plan.Filter.ElementCount > 0)
            prelude.Add(new BsonDocument("$match", plan.Filter));

        var countPipeline = new List<BsonDocument>(prelude) { new BsonDocument("$count", "total") };
        var countDoc = collection
            .Aggregate<BsonDocument>(PipelineDefinition<TDocument, BsonDocument>.Create(countPipeline))
            .FirstOrDefault();
        var total = countDoc is null ? 0 : countDoc["total"].ToInt64();

        var dataPipeline = new List<BsonDocument>(prelude);
        if (plan.Sort is not null)
            dataPipeline.Add(new BsonDocument("$sort", plan.Sort)); // sort esplicito; se assente vince la rilevanza Atlas
        dataPipeline.Add(new BsonDocument("$skip", plan.Skip));
        dataPipeline.Add(new BsonDocument("$limit", plan.Limit));
        dataPipeline.Add(new BsonDocument("$project", plan.Projection));

        var documents = collection
            .Aggregate<BsonDocument>(PipelineDefinition<TDocument, BsonDocument>.Create(dataPipeline))
            .ToList();

        return Build(documents, plan, page, total);
    }

    private static SearchResult<IReadOnlyDictionary<string, object?>> Build(
        List<BsonDocument> documents, MongoQueryPlan plan, PageRequest page, long total)
    {
        var items = documents
            .Select(doc => (IReadOnlyDictionary<string, object?>)MapRecord(doc, plan.Fields))
            .ToList();

        return new SearchResult<IReadOnlyDictionary<string, object?>>(items, total, page.Number, page.Size);
    }

    /// <summary>Costruisce la query Mongo senza eseguirla (per ispezione/test).</summary>
    public MongoQueryPlan BuildPlan(SearchRequest request)
    {
        var filter = request.Filter is null
            ? new BsonDocument()
            : _filterTranslator.BuildFilterDocument(request.Filter);

        var searchStage = BuildSearchStage(request.Search);
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
        if (string.IsNullOrWhiteSpace(searchText))
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
            var path = PathOf(name);
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
            document[PathOf(sort.Field)] = sort.Direction == Search.Core.SortDirection.Ascending ? 1 : -1;
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
