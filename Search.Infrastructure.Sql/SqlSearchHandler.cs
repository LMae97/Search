using Search.Application.Querying;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Metadata;

namespace Search.Infrastructure.Sql;

/// <summary>
/// Handler di ricerca per lo store <b>PostgresRaw</b>: serve <b>tutte</b> le entità di quello store
/// (product, brand, …), risolvendo schema e mappa per nome a runtime. Traduce l'albero in SQL testuale
/// parametrizzato ed esegue via ADO.NET. La pipeline comune (permessi/potatura/validazione) è nella base.
/// </summary>
public sealed class SqlSearchHandler(
    DbBackedSearchMapProvider maps,
    ISqlSchemaProvider schemas,
    ICatalogConnectionFactory connections) : SearchHandlerBase(maps)
{
    public override StoreKind Store => StoreKind.PostgresRaw;

    protected override SearchResult<IReadOnlyDictionary<string, object?>> Execute(string entityName, IEntitySearchMap map, SearchRequest request)
    {
        var builder = new SqlSearchQueryBuilder(map, schemas.GetSchema(entityName));
        var executor = new SqlSearchExecutor();

        using var connection = connections.Create();
        connection.Open();

        var total = executor.Count(connection, builder.BuildCount(request));
        var items = executor.Query(connection, builder.Build(request));

        return new SearchResult<IReadOnlyDictionary<string, object?>>(items, total, request.Page.Number, request.Page.Size);
    }
}