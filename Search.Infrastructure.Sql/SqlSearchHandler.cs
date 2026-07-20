using Search.Application.Querying;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Metadata;

namespace Search.Infrastructure.Sql;

/// <summary>
/// Handler di ricerca per le entità dello store <b>PostgresRaw</b> (parametrizzato per nome: vale per
/// product, brand, …). Traduce l'albero in SQL testuale parametrizzato ed esegue via ADO.NET. La pipeline
/// comune (permessi/potatura/validazione) è nella base; qui c'è solo l'esecuzione.
/// </summary>
public sealed class SqlSearchHandler(
    string entityName,
    DbBackedSearchMapProvider maps,
    ISqlSchemaProvider schemas,
    ICatalogConnectionFactory connections) : SearchHandlerBase(entityName, maps)
{
    protected override SearchResult<IReadOnlyDictionary<string, object?>> Execute(IEntitySearchMap map, SearchRequest request)
    {
        var builder = new SqlSearchQueryBuilder(map, schemas.GetSchema(EntityName));
        var executor = new SqlSearchExecutor();

        using var connection = connections.Create();
        connection.Open();

        var total = executor.Count(connection, builder.BuildCount(request));
        var items = executor.Query(connection, builder.Build(request));

        return new SearchResult<IReadOnlyDictionary<string, object?>>(items, total, request.Page.Number, request.Page.Size);
    }
}
