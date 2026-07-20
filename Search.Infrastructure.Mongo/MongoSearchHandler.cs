using MongoDB.Bson;
using Search.Application.Querying;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Metadata;

namespace Search.Infrastructure.Mongo;

/// <summary>
/// Handler di ricerca per lo store <b>Mongo</b>: serve tutte le entità documentali, risolvendo mappa e
/// collection per nome a runtime. Delega a <see cref="MongoSearchExecutor{TDocument}"/> (filtro → BsonDocument,
/// proiezione/sort/paginazione). La pipeline comune (permessi/potatura/validazione) è nella base.
/// </summary>
public sealed class MongoSearchHandler(
    DbBackedSearchMapProvider maps,
    IMongoCollectionProvider collections) : SearchHandlerBase(maps)
{
    public override StoreKind Store => StoreKind.Mongo;

    protected override SearchResult<IReadOnlyDictionary<string, object?>> Execute(string entityName, IEntitySearchMap map, SearchRequest request)
    {
        var collection = collections.GetCollection(entityName);
        return new MongoSearchExecutor<BsonDocument>(map).Execute(collection, request);
    }
}
