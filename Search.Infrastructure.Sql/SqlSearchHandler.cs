using Microsoft.Extensions.Logging;
using WeByte.Search.Application.Config;
using WeByte.Search.Application.Querying.Dynamic;
using WeByte.Search.Application.Search;
using WeByte.Search.Core;
using WeByte.Search.Core.Dynamic;
using WeByte.Search.Core.Metadata;
using WeByte.Search.Sql;

namespace WeByte.Search.Infrastructure.Sql;

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

    protected override SearchResult<IReadOnlyDictionary<string, object?>> Execute(ISearchableEntityConfig config, IEntitySearchMap map, SearchRequest request, Guid spaceId)
    {
        var entityName = config.SearchEntity.Name;
        var builder = new SqlSearchQueryBuilder(map, schemas.GetSchema(entityName));
        var executor = new SqlSearchExecutor();

        using var connection = connections.Create();
        connection.Open();

        // Il conteggio è responsabilità di ExecuteCount, chiamato a parte da SearchHandlerBase.SearchWithCount:
        // qui si eseguono SOLO i dati, altrimenti si pagherebbe la stessa query di conteggio due volte.
        var query = builder.Build(request, spaceId);

        logger.LogInformation("Executing SQL query:\n{Sql}\nwith parameters:\n{Parameters}", query.Sql, FormatParameters(query.Parameters));

        var items = executor.Query(connection, query);

        return new SearchResult<IReadOnlyDictionary<string, object?>>(items);
    }

    protected override long ExecuteCount(ISearchableEntityConfig config, IEntitySearchMap map, SearchRequest request, Guid spaceId, long? upTo)
    {
        var builder = new SqlSearchQueryBuilder(map, schemas.GetSchema(config.SearchEntity.Name));
        var executor = new SqlSearchExecutor();

        using var connection = connections.Create();
        connection.Open();

        var countQuery = builder.BuildCount(request, spaceId, upTo);

        logger.LogInformation("Executing SQL count query:\n{Sql}\nwith parameters:\n{Parameters}", countQuery.Sql, FormatParameters(countQuery.Parameters));

        return executor.Count(connection, countQuery);
    }

    //TODO: SERVE SOLO PER I LOG
    private static string FormatParameters(IReadOnlyDictionary<string, object?> parameters)
        => parameters.Count == 0
            ? "(nessuno)"
            : string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value ?? "NULL"}"));
}