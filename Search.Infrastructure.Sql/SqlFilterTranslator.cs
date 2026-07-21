using Search.Application.Querying.Filters;
using Search.Application.Querying.Metadata;

namespace Search.Infrastructure.Sql;

/// <summary>
/// Traduce l'albero di filtri store-agnostic in un frammento di <b>SQL testuale parametrizzato</b>
/// (la clausola WHERE), simmetrico a <c>LinqFilterTranslator</c> (Expression) e
/// <c>MongoFilterTranslator</c> (BsonDocument).
/// <para>
/// <b>Indipendenza dal modello</b>: non conosce classi CLR né EF. Ogni campo porta nei metadati la
/// propria espressione-colonna SQL (<see cref="FieldDescriptor.StoragePath"/>, es. <c>"brand"."Code"</c>),
/// presa dalla whitelist → fidata, sicura da interpolare. I <b>valori</b> del filtro, che arrivano
/// dall'utente, non vengono <b>mai</b> interpolati: diventano parametri (<c>@p0, @p1, …</c>) → niente
/// SQL injection e niente problemi di quoting/tipi.
/// </para>
/// </summary>
public sealed class SqlFilterTranslator
{
    private readonly IEntitySearchMap _map;
    private readonly IReadOnlyDictionary<string, SqlArrayMapping> _arrayMappings;

    /// <param name="map">Mappa effettiva dei campi (già filtrata per permessi/tenant).</param>
    /// <param name="arrayMappings">Mappatura per-campo delle collezioni (M2M o JSON) → come diventano un EXISTS
    /// correlato; dalla config SQL dell'entità. Null = l'entità non ha campi array ricercabili.</param>
    public SqlFilterTranslator(IEntitySearchMap map, IReadOnlyDictionary<string, SqlArrayMapping>? arrayMappings = null)
    {
        _map = map;
        _arrayMappings = arrayMappings ?? new Dictionary<string, SqlArrayMapping>();
    }

    /// <summary>Albero → clausola SQL + valori parametrizzati.</summary>
    public SqlFilter Translate(FilterNode node)
    {
        var parameters = new List<object?>();
        var sql = Build(node, parameters);
        return new SqlFilter(sql, parameters);
    }

    private string Build(FilterNode node, List<object?> parameters) => node switch
    {
        LogicalFilterNode logical => BuildLogical(logical, parameters),
        ComparisonFilterNode comparison => BuildComparison(comparison, parameters),
        _ => throw new NotSupportedException($"Nodo di filtro non supportato: {node.GetType().Name}.")
    };

    private string BuildLogical(LogicalFilterNode logical, List<object?> parameters)
    {
        var children = logical.Children.Select(child => Build(child, parameters)).ToList();

        return logical.Operator switch
        {
            LogicalOperator.And => $"({string.Join(" AND ", children)})",
            LogicalOperator.Or => $"({string.Join(" OR ", children)})",
            LogicalOperator.Not => $"NOT ({children[0]})",
            _ => throw new NotSupportedException($"Operatore logico non supportato: {logical.Operator}.")
        };
    }

    private string BuildComparison(ComparisonFilterNode node, List<object?> parameters)
    {
        if (!_map.TryGetField(node.Field, out var field))
            throw new InvalidOperationException($"Filtro su campo non mappato '{node.Field}'.");

        return field.IsArray
            ? BuildArray(node, field, parameters)
            : BuildScalar(node, field, parameters);
    }

    // --- Scalari: <colonna> <operatore> <parametro> ------------------------------------------------

    private string BuildScalar(ComparisonFilterNode node, FieldDescriptor field, List<object?> parameters)
    {
        var column = field.SqlColumn();

        return node.Operator switch
        {
            // Equals/NotEquals con valore null → IS [NOT] NULL (in SQL "= NULL" è sempre falso).
            FilterOperator.Equals when node.SingleValue is null => $"{column} IS NULL",
            FilterOperator.NotEquals when node.SingleValue is null => $"{column} IS NOT NULL",

            FilterOperator.Equals => $"{column} = {Param(parameters, node.SingleValue)}",
            FilterOperator.NotEquals => $"{column} <> {Param(parameters, node.SingleValue)}",
            FilterOperator.GreaterThan => $"{column} > {Param(parameters, node.SingleValue)}",
            FilterOperator.GreaterThanOrEqual => $"{column} >= {Param(parameters, node.SingleValue)}",
            FilterOperator.LessThan => $"{column} < {Param(parameters, node.SingleValue)}",
            FilterOperator.LessThanOrEqual => $"{column} <= {Param(parameters, node.SingleValue)}",
            FilterOperator.Between => $"{column} BETWEEN {Param(parameters, node.Values[0])} AND {Param(parameters, node.Values[1])}",

            FilterOperator.In => $"{column} IN ({ParamList(parameters, node.Values)})",
            FilterOperator.NotIn => $"{column} NOT IN ({ParamList(parameters, node.Values)})",

            // Case-insensitive (lower su entrambi i lati) come LINQ e Mongo. LikeTerm "neutralizza" i jolly
            // (%, _) nel termine → un valore che li contiene è cercato alla lettera (vedi Like + ESCAPE).
            FilterOperator.Contains => Like(column, parameters, $"%{LikeTerm(node.SingleValue)}%"),
            FilterOperator.NotContains => $"NOT ({Like(column, parameters, $"%{LikeTerm(node.SingleValue)}%")})",
            FilterOperator.StartsWith => Like(column, parameters, $"{LikeTerm(node.SingleValue)}%"),
            FilterOperator.EndsWith => Like(column, parameters, $"%{LikeTerm(node.SingleValue)}"),

            FilterOperator.IsNull => $"{column} IS NULL",
            FilterOperator.IsNotNull => $"{column} IS NOT NULL",

            _ => throw new NotSupportedException(
                $"Operatore '{node.Operator}' non supportato per il campo scalare '{field.Name}'.")
        };
    }

    // --- Array/collezione (M2M): EXISTS correlato -------------------------------------------------

    private string BuildArray(ComparisonFilterNode node, FieldDescriptor field, List<object?> parameters)
    {
        if (!_arrayMappings.TryGetValue(field.Name, out var join))
            throw new NotSupportedException(
                $"Il campo array '{field.Name}' non ha una mappatura di join SQL: va definita nell'adapter (SqlArrayMapping).");

        // Corpo comune "SELECT 1 <FROM/JOIN + correlazione col padre>"; il predicato sull'elemento è opzionale.
        string Exists(string? elementPredicate) => elementPredicate is null
            ? $"EXISTS (SELECT 1 {join.From})"
            : $"EXISTS (SELECT 1 {join.From} AND {elementPredicate})";

        return node.Operator switch
        {
            FilterOperator.ArrayContains => Exists($"{join.ElementColumn} = {Param(parameters, node.SingleValue)}"),
            FilterOperator.ArrayContainsAny => Exists($"{join.ElementColumn} IN ({ParamList(parameters, node.Values)})"),
            // "contiene TUTTI": un EXISTS per ciascun valore, in AND (ognuno deve trovare almeno una riga).
            FilterOperator.ArrayContainsAll => "(" + string.Join(" AND ",
                node.Values.Select(value => Exists($"{join.ElementColumn} = {Param(parameters, value)}"))) + ")",
            FilterOperator.ArrayIsEmpty => $"NOT {Exists(null)}",
            FilterOperator.ArrayNotEmpty => Exists(null),
            _ => throw new NotSupportedException(
                $"Operatore '{node.Operator}' non supportato per il campo array '{field.Name}'.")
        };
    }

    // --- Parametri: i valori utente non si interpolano mai. Nome posizionale @p0, @p1, … -----------

    private static string Param(List<object?> parameters, object? value)
    {
        var name = "@p" + parameters.Count;
        parameters.Add(value);
        return name;
    }

    private static string ParamList(List<object?> parameters, IReadOnlyList<object?> values) =>
        string.Join(", ", values.Select(value => Param(parameters, value)));

    // LIKE case-insensitive: lower() su entrambi i lati. ESCAPE '\' abbinato a LikeTerm: chi cerca "50%"
    // trova il letterale, non "50 seguito da qualsiasi cosa".
    private static string Like(string column, List<object?> parameters, string pattern) =>
        $"lower({column}) LIKE {Param(parameters, pattern)} ESCAPE '\\'";

    // Termine per LIKE: minuscolo + escape dei metacaratteri (PRIMA il backslash, poi i jolly % e _).
    private static string LikeTerm(object? value) =>
        (value?.ToString()?.ToLowerInvariant() ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
}

/// <summary>Frammento SQL parametrizzato: la clausola (senza <c>WHERE</c>) e i valori dei parametri (<c>@p0..@pN</c>).</summary>
public sealed record SqlFilter(string Sql, IReadOnlyList<object?> Parameters);

public abstract record SqlJoin;

public sealed record SqlSimpleJoin(string Condition) : SqlJoin;

/// <summary>
/// Come un campo array (collezione M2M) diventa un <c>EXISTS</c> correlato in SQL. Vive nell'adapter SQL,
/// non nei metadati store-agnostic: la forma relazionale — tabella ponte, chiavi, correlazione — è un
/// dettaglio d'infrastruttura, esattamente come la mappatura fluent di EF sta nel <c>DbContext</c>.
/// </summary>
/// <param name="From">Tutto ciò che segue <c>SELECT 1</c>: <c>FROM</c>/<c>JOIN</c> + <c>WHERE</c> di correlazione col padre.
/// Per una M2M è la join sulla tabella-ponte; per un array JSON è un unnest (<c>jsonb_array_elements[_text]</c>) chiuso da <c>WHERE true</c>.</param>
/// <param name="ElementColumn">Colonna/estrazione dell'elemento su cui si applica il predicato (es. <c>"tag"."Name"</c> o <c>e ->> 'sku'</c>).</param>
/// <param name="Projection">
/// Come proiettare l'intero array nel <c>SELECT</c>. <c>null</c> ⇒ lo si RICOSTRUISCE con <c>json_group_array(ElementColumn)</c>
/// sullo stesso join (caso M2M). Valorizzato ⇒ espressione diretta (es. <c>"brand"."Data" -> 'tags'</c>: la colonna JSON È già l'array).
/// </param>
public sealed record SqlArrayMapping(string From, string ElementColumn, string? Projection = null) : SqlJoin;