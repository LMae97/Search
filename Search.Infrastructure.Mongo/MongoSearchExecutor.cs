using MongoDB.Bson;
using MongoDB.Driver;
using Search.Application.Querying;
using Search.Application.Querying.Metadata;

namespace Search.Infrastructure.Mongo;

/// <summary>
/// Esegue una <see cref="SearchRequest"/> su una collezione MongoDB — il gemello documentale di
/// <c>LinqSearchExecutor</c>. Filtro, ordinamento, proiezione e paginazione diventano una
/// <c>find</c> Mongo. La proiezione dinamica qui è banale: un <b>projection document</b> con i soli
/// path richiesti (niente over-fetch, niente tipi statici). Ritorna lo stesso
/// <see cref="SearchResult{T}"/> di dizionari del lato relazionale.
/// </summary>
public sealed class MongoSearchExecutor<TDocument>
{
    private readonly IEntitySearchMap _map;
    private readonly MongoFilterTranslator<TDocument> _filterTranslator;

    public MongoSearchExecutor(IEntitySearchMap map)
    {
        _map = map;
        _filterTranslator = new MongoFilterTranslator<TDocument>(map);
    }

    public SearchResult<IReadOnlyDictionary<string, object?>> Execute(IMongoCollection<TDocument> collection, SearchRequest request)
    {
        var plan = BuildPlan(request);

        var total = collection.CountDocuments(plan.Filter);

        var find = collection.Find(plan.Filter);
        if (plan.Sort is not null)
            find = find.Sort(plan.Sort);

        var documents = find
            .Skip(plan.Skip)
            .Limit(plan.Limit)
            .Project<BsonDocument>(plan.Projection)
            .ToList();

        var items = documents
            .Select(doc => (IReadOnlyDictionary<string, object?>)MapRecord(doc, plan.Fields))
            .ToList();

        return new SearchResult<IReadOnlyDictionary<string, object?>>(items, total, request.Page.Number, request.Page.Size);
    }

    /// <summary>Costruisce la query Mongo senza eseguirla (per ispezione/test).</summary>
    public MongoQueryPlan BuildPlan(SearchRequest request)
    {
        var filter = request.Filter is null
            ? new BsonDocument()
            : _filterTranslator.BuildFilterDocument(request.Filter);

        var (projection, fields) = BuildProjection(request.Projection);
        var sort = BuildSort(request.Sort);

        return new MongoQueryPlan(filter, projection, sort, request.Page.Skip, request.Page.Size, fields);
    }

    private (BsonDocument Projection, IReadOnlyList<(string Name, string Path)> Fields) BuildProjection(IReadOnlyList<string> projection)
    {
        var names = projection.Count == 0 ? _map.DefaultProjection() : projection;

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

    private BsonDocument? BuildSort(IReadOnlyList<SortField> sorts)
    {
        if (sorts.Count == 0)
        {
            // Nessun sort esplicito → default deterministico (id/createdAt) se l'entità ce l'ha, come gli altri store.
            var def = _map.DefaultSortField();
            return def is null ? null : new BsonDocument { [PathOf(def.Name)] = 1 };
        }

        var document = new BsonDocument();
        foreach (var sort in sorts)
            document[PathOf(sort.Field)] = sort.Direction == Search.Application.Querying.SortDirection.Ascending ? 1 : -1;
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
public sealed record MongoQueryPlan(
    BsonDocument Filter,
    BsonDocument Projection,
    BsonDocument? Sort,
    int Skip,
    int Limit,
    IReadOnlyList<(string Name, string Path)> Fields);
