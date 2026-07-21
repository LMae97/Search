using Microsoft.Extensions.Logging;
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
    ICatalogConnectionFactory connections,
    ILogger<SqlSearchHandler> logger) : SearchHandlerBase(maps)
{
    public override StoreKind Store => StoreKind.PostgresRaw;

    protected override SearchResult<IReadOnlyDictionary<string, object?>> Execute(string entityName, IEntitySearchMap map, SearchRequest request)
    {
        var builder = new SqlSearchQueryBuilder(map, schemas.GetSchema(entityName));
        var executor = new SqlSearchExecutor();

        using var connection = connections.Create();
        connection.Open();

        var countQuery = builder.BuildCount(request);
        var query = builder.Build(request);

        logger.LogInformation("Executing SQL count query:\n{Sql}\nwith parameters:\n{Parameters}", countQuery.Sql, FormatParameters(countQuery.Parameters));
        logger.LogInformation("Executing SQL query:\n{Sql}\nwith parameters:\n{Parameters}", query.Sql, FormatParameters(query.Parameters));

        var total = executor.Count(connection, countQuery);
        var items = executor.Query(connection, query);

        return new SearchResult<IReadOnlyDictionary<string, object?>>(items, total, request.Page.Number, request.Page.Size);
    }

    //TODO: SERVE SOLO PER I LOG
    private static string FormatParameters(IReadOnlyDictionary<string, object?> parameters)
        => parameters.Count == 0
            ? "(nessuno)"
            : string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value ?? "NULL"}"));
}