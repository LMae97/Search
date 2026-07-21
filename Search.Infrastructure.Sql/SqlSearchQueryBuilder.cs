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
    private readonly SqlEntitySchema _schema;     // config SQL dell'entità: FROM base, soft-delete, join collezioni
    private readonly SqlFilterTranslator _filter;

    public SqlSearchQueryBuilder(IEntitySearchMap map, SqlEntitySchema schema)
    {
        _map = map;
        _schema = schema;
        _filter = new SqlFilterTranslator(map, schema.GetM2MJoins());
    }

    /// <summary>Query dati: <c>SELECT … FROM … WHERE … ORDER BY … LIMIT/OFFSET</c>.</summary>
    public SqlQueryPlan Build(SearchRequest request)
    {
        var names = ResolveProjection(request.Projection);
        var parameters = new Dictionary<string, object?>();

        var select = string.Join(",\n       ", names.Select(SelectColumn));
        // FROM con i soli join richiesti dai campi effettivamente usati (proiezione ∪ filtro ∪ sort).
        var from = BuildFrom(names.Concat(FilterFields(request.Filter)).Concat(request.Sort.Select(sort => sort.Field)));
        var where = BuildWhere(request.Filter, parameters);
        var orderBy = BuildOrderBy(request.Sort);

        // Paginazione: parametri NOMINALI (@skip/@take) per non collidere con i @pN posizionali del filtro.
        parameters["@skip"] = request.Page.Skip;
        parameters["@take"] = request.Page.Size;

        var sql =
            $"SELECT {select}\n" +
            $"{from}\n" +
            where +
            orderBy +
            "LIMIT @take OFFSET @skip";

        return new SqlQueryPlan(sql, parameters);
    }

    /// <summary>Query di conteggio totale (stesso WHERE, senza proiezione/ordinamento/paginazione).</summary>
    public SqlQueryPlan BuildCount(SearchRequest request)
    {
        var parameters = new Dictionary<string, object?>();
        var where = BuildWhere(request.Filter, parameters);
        // Il COUNT non proietta né ordina: servono solo i join richiesti dai campi del filtro.
        var from = BuildFrom(FilterFields(request.Filter));
        var sql = $"SELECT COUNT(*)\n{from}\n{where}".TrimEnd();
        return new SqlQueryPlan(sql, parameters);
    }

    private IReadOnlyList<string> ResolveProjection(IReadOnlyList<string> projection) =>
        projection.Count == 0 ? [] : projection.ToList();

    // FROM base + i soli join dei campi usati (deduplicati). Come l'EXISTS dei tag: si paga solo se serve.
    private string BuildFrom(IEnumerable<string> usedFields)
    {
        if (_schema.GetSimpleJoins().Count == 0)
            return _schema.From;

        var scalarJoins = _schema.GetSimpleJoins();

        var joins = usedFields
            .Where(scalarJoins.ContainsKey)
            .Select(field => scalarJoins[field].From)
            .Distinct()
            .ToList();

        return joins.Count == 0 ? _schema.From : $"{_schema.From}\n{string.Join("\n", joins)}";
    }

    // Nomi dei campi referenziati dall'albero di filtri (per sapere quali join servono al WHERE).
    private static IEnumerable<string> FilterFields(FilterNode? node) => node switch
    {
        ComparisonFilterNode comparison => [comparison.Field],
        LogicalFilterNode logical => logical.Children.SelectMany(FilterFields),
        _ => []
    };

    private string SelectColumn(string name)
    {
        if (!_map.TryGetField(name, out var field))
            throw new InvalidOperationException($"Proiezione su campo non mappato '{name}'.");

        // Campo array/collezione, due "facce" (vedi SqlM2MJoin):
        //  - M2M (junction): niente Projection → RICOSTRUISCE l'array con json_group_array sullo stesso join del filtro.
        //  - JSON (jsonb): la colonna È già un array → Projection diretta (es. "brand"."Data" -> 'tags').

        if (!field.IsArray || field.JsonColumn)
            return $"{field.SqlColumn()} AS \"{name}\"";

        if (!_schema.GetM2MJoins().TryGetValue(name, out var join))
            throw new NotSupportedException($"Il campo array '{name}' non ha una mappatura di collezione SQL (SqlArrayMapping).");

        return $"(SELECT json_agg({field.SqlColumn()}) {join.From}) AS \"{name}\"";
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
        {
            // Nessun sort esplicito → default deterministico (id/createdAt) se l'entità ce l'ha, come gli altri store.
            var def = _map.DefaultSortField();
            return def is null ? string.Empty : $"ORDER BY {def.SqlColumn()} ASC\n";
        }

        var parts = sorts.Select(sort =>
        {
            if (!_map.TryGetField(sort.Field, out var field))
                throw new InvalidOperationException($"Ordinamento su campo non mappato '{sort.Field}'.");
            if (field.IsArray)
                throw new NotSupportedException($"Ordinamento non supportato sul campo array '{sort.Field}'.");
            var direction = sort.Direction == SortDirection.Ascending ? "ASC" : "DESC";
            return $"{field.SqlColumn()} {direction}";
        });

        return $"ORDER BY {string.Join(", ", parts)}\n";
    }
}

/// <summary>Query SQL completa e parametrizzata + i nomi dei campi proiettati (nell'ordine del SELECT). Gemello di <c>MongoQueryPlan</c>.</summary>
public sealed record SqlQueryPlan(string Sql, IReadOnlyDictionary<string, object?> Parameters);
