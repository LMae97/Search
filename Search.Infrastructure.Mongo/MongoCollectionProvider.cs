using MongoDB.Bson;
using MongoDB.Driver;

namespace Search.Infrastructure.Mongo;

/// <summary>
/// Fornisce la <see cref="IMongoCollection{TDocument}"/> (schemaless, <see cref="BsonDocument"/>) per
/// un'entità di ricerca. Astratto così l'handler resta agnostico da come si risolve/nomina la collection.
/// </summary>
public interface IMongoCollectionProvider
{
    IMongoCollection<BsonDocument> GetCollection(string entityName);
}

/// <summary>
/// Risolve la collection dal database: usa il nome mappato per l'entità (es. <c>"order" → "orders"</c>),
/// altrimenti il nome dell'entità così com'è.
/// </summary>
public sealed class MongoCollectionProvider(
    IMongoDatabase database,
    IReadOnlyDictionary<string, string>? collectionNames = null) : IMongoCollectionProvider
{
    private readonly IReadOnlyDictionary<string, string> _names =
        collectionNames ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IMongoCollection<BsonDocument> GetCollection(string entityName)
    {
        var name = _names.TryGetValue(entityName, out var mapped) ? mapped : entityName;
        return database.GetCollection<BsonDocument>(name);
    }
}
