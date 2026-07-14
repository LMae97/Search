using Search.Application.Querying;
using Search.Application.Querying.Filters;
using Search.Application.Querying.Metadata;

namespace Search.Infrastructure.Sql;

/// <summary>
/// Assembla una query SQL <b>completa e parametrizzata</b> per uno store "raw" (<see cref="Dynamic.StoreKind.PostgresRaw"/>):
/// SELECT (proiezione), FROM (base), WHERE (dal <see cref="SqlFilterTranslator"/>), ORDER BY, LIMIT/OFFSET.
/// Nessun modello CLR/EF: solo i metadati dei campi. È l'equivalente "testo SQL" di
/// <c>LinqSearchExecutor</c> (Expression) e <c>MongoSearchExecutor</c> (BsonDocument).
/// </summary>
public sealed class SqlSearchQueryBuilder
{
    private readonly IEntitySearchMap _map;
    private readonly SqlEntitySchema _schema;    // config SQL dell'entità: FROM base, soft-delete, join collezioni
    private readonly SqlFilterTranslator _filter;

    public SqlSearchQueryBuilder(IEntitySearchMap map, SqlEntitySchema schema)
    {
        _map = map;
        _schema = schema;
        _filter = new SqlFilterTranslator(map, schema.ArrayFilters);
    }

    /// <summary>Query dati: <c>SELECT … FROM … WHERE … ORDER BY … LIMIT/OFFSET</c>.</summary>
    public SqlQuery Build(SearchRequest request)
    {
        var names = ResolveProjection(request.Projection);
        var parameters = new Dictionary<string, object?>();

        var select = string.Join(",\n       ", names.Select(SelectColumn));
        var where = BuildWhere(request.Filter, parameters);
        var orderBy = BuildOrderBy(request.Sort);

        // Paginazione: parametri NOMINALI (@skip/@take) per non collidere con i @pN posizionali del filtro.
        parameters["@skip"] = request.Page.Skip;
        parameters["@take"] = request.Page.Size;

        var sql =
            $"SELECT {select}\n" +
            $"{_schema.From}\n" +
            where +
            orderBy +
            "LIMIT @take OFFSET @skip";

        return new SqlQuery(sql, parameters, names);
    }

    /// <summary>Query di conteggio totale (stesso WHERE, senza proiezione/ordinamento/paginazione).</summary>
    public SqlQuery BuildCount(SearchRequest request)
    {
        var parameters = new Dictionary<string, object?>();
        var where = BuildWhere(request.Filter, parameters);
        var sql = $"SELECT COUNT(*)\n{_schema.From}\n{where}".TrimEnd();
        return new SqlQuery(sql, parameters, ["count"]);
    }

    private IReadOnlyList<string> ResolveProjection(IReadOnlyList<string> projection) =>
        projection.Count == 0
            ? _map.Fields.Values.Where(field => field.VisibleByDefault).Select(field => field.Name).ToList()
            : projection.ToList();

    private string SelectColumn(string name)
    {
        if (!_map.TryGetField(name, out var field))
            throw new InvalidOperationException($"Proiezione su campo non mappato '{name}'.");

        if (!field.IsArray)
            return $"{Column(field)} AS \"{name}\"";

        // Campo array/collezione, due "facce" (vedi SqlArrayFilter):
        //  - M2M (junction): niente Projection → RICOSTRUISCE l'array con json_group_array sullo stesso join del filtro.
        //  - JSON (jsonb): la colonna È già un array → Projection diretta (es. "brand"."Data" -> 'tags').
        if (!_schema.ArrayFilters.TryGetValue(name, out var join))
            throw new NotSupportedException($"Il campo array '{name}' non ha una mappatura di join SQL (SqlArrayFilter).");
        var projection = join.Projection ?? $"(SELECT json_group_array({join.ElementColumn}) {join.From})";
        return $"{projection} AS \"{name}\"";
    }

    private string BuildWhere(FilterNode? filter, Dictionary<string, object?> parameters)
    {
        var clauses = new List<string>();
        if (_schema.BasePredicate is not null)
            clauses.Add(_schema.BasePredicate);

        if (filter is not null)
        {
            var translated = _filter.Translate(filter);
            // I parametri posizionali del filtro (@p0..@pN) confluiscono nel dizionario comune, con lo stesso nome.
            for (var i = 0; i < translated.Parameters.Count; i++)
                parameters["@p" + i] = translated.Parameters[i];
            clauses.Add(translated.Sql);
        }

        return clauses.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", clauses)}\n";
    }

    private string BuildOrderBy(IReadOnlyList<SortField> sorts)
    {
        if (sorts.Count == 0)
            return string.Empty;

        var parts = sorts.Select(sort =>
        {
            if (!_map.TryGetField(sort.Field, out var field))
                throw new InvalidOperationException($"Ordinamento su campo non mappato '{sort.Field}'.");
            if (field.IsArray)
                throw new NotSupportedException($"Ordinamento non supportato sul campo array '{sort.Field}'.");
            var direction = sort.Direction == SortDirection.Ascending ? "ASC" : "DESC";
            return $"{Column(field)} {direction}";
        });

        return $"ORDER BY {string.Join(", ", parts)}\n";
    }

    private static string Column(FieldDescriptor field) =>
        field.StoragePath
        ?? throw new NotSupportedException($"Il campo '{field.Name}' non ha una colonna SQL (StoragePath).");
}

/// <summary>Query SQL completa e parametrizzata + i nomi dei campi proiettati (nell'ordine del SELECT).</summary>
public sealed record SqlQuery(string Sql, IReadOnlyDictionary<string, object?> Parameters, IReadOnlyList<string> Fields);
