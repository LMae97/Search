using System.Text.Json;
using Search.Core;
using Search.Core.Filters;

namespace Search.Api.Dto;

/// <summary>
/// Traduce la parte <b>comune a ogni entità</b> di <see cref="BaseSearchRequestDto"/> (ricerca libera,
/// colonne, ordinamento, paginazione, filtri generici) in un <see cref="SearchRequest"/>. Le entità
/// specifiche (contratti, utenti, ...) chiamano <see cref="ToSearchRequest"/> e poi aggiungono i propri
/// filtri tipizzati in AND, con lo stesso pattern del vecchio <c>SearchContractBaseFilters.GetBaseFilters</c>
/// — la differenza è che qui il risultato è un <see cref="FilterNode"/> del motore, non l'Filter del vecchio
/// acquario-be.
/// </summary>
public static class BaseSearchRequestDtoAdapter
{
    /// <summary>AND delle liste esterne, OR di ciascuna lista interna. Esposto per riuso dai filtri "extra".</summary>
    public static FilterNode? BuildFilter(List<List<FilterDto>>? filters)
    {
        if (filters is null) return null;

        var ors = filters
            .Where(group => group is { Count: > 0 })
            .Select(CombineOr)
            .ToList();

        return Combine(LogicalOperator.And, ors);
    }

    /// <summary>AND di più nodi opzionali, collassando i casi 0/1 nodo (nessun wrapper superfluo).</summary>
    public static FilterNode? Combine(LogicalOperator op, IReadOnlyList<FilterNode?> nodes)
    {
        var present = nodes.Where(n => n is not null).Select(n => n!).ToList();
        return present.Count switch
        {
            0 => null,
            1 => present[0],
            _ => op == LogicalOperator.And ? Filter.And(present.ToArray()) : Filter.Or(present.ToArray())
        };
    }

    private static FilterNode CombineOr(List<FilterDto> group)
    {
        var nodes = group.Select(ToComparison).ToArray();
        return nodes.Length == 1 ? nodes[0] : Filter.Or(nodes);
    }

    private static FilterNode ToComparison(FilterDto f)
    {
        var op = ParseOperation(f.Operation);

        return op switch
        {
            FilterOperator.IsNull => Filter.IsNull(f.Field),
            FilterOperator.IsNotNull => Filter.IsNotNull(f.Field),
            FilterOperator.Between => BuildBetween(f),
            FilterOperator.In => Filter.In(f.Field, SplitValues(f.Value)),
            FilterOperator.NotIn => Filter.NotIn(f.Field, SplitValues(f.Value)),
            FilterOperator.ArrayContainsAny => Filter.ArrayContainsAny(f.Field, SplitValues(f.Value)),
            FilterOperator.ArrayContainsAll => Filter.ArrayContainsAll(f.Field, SplitValues(f.Value)),
            FilterOperator.ArrayIsEmpty => Filter.ArrayIsEmpty(f.Field),
            FilterOperator.ArrayNotEmpty => Filter.ArrayNotEmpty(f.Field),
            _ => new ComparisonFilterNode(f.Field, op, f.Value)
        };
    }

    private static FilterNode BuildBetween(FilterDto f)
    {
        var values = SplitValues(f.Value);
        if (values.Length != 2)
            throw new ArgumentException($"L'operatore 'between' richiede esattamente 2 valori separati da virgola (campo '{f.Field}').");
        return Filter.Between(f.Field, values[0], values[1]);
    }

    // Valori multipli: accetta sia CSV ("a,b,c") sia un array JSON serializzato in stringa
    // ("[\"a\",\"b\"]") — quest'ultimo perché è così che il FE reale li manda, non come CSV.
    private static object?[] SplitValues(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<string?>>(trimmed);
                return items?.Cast<object?>().ToArray() ?? [];
            }
            catch (JsonException)
            {
                // Non era JSON valido nonostante le parentesi: ricadi sullo split a virgola sotto.
            }
        }

        return trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Cast<object?>()
            .ToArray();
    }

    private static FilterOperator ParseOperation(string operation) => operation.Trim().ToLowerInvariant() switch
    {
        "eq" or "equals" => FilterOperator.Equals,
        "neq" or "ne" or "notequals" => FilterOperator.NotEquals,
        "gt" => FilterOperator.GreaterThan,
        "gte" => FilterOperator.GreaterThanOrEqual,
        "lt" => FilterOperator.LessThan,
        "lte" => FilterOperator.LessThanOrEqual,
        "between" => FilterOperator.Between,
        "in" => FilterOperator.In,
        "nin" or "notin" => FilterOperator.NotIn,
        "contains" => FilterOperator.Contains,
        "ncontains" or "notcontains" => FilterOperator.NotContains,
        "startswith" => FilterOperator.StartsWith,
        "endswith" => FilterOperator.EndsWith,
        "null" or "isnull" => FilterOperator.IsNull,
        "notnull" or "isnotnull" => FilterOperator.IsNotNull,
        "arraycontains" => FilterOperator.ArrayContains,
        "containsany" => FilterOperator.ArrayContainsAny,
        "containsall" => FilterOperator.ArrayContainsAll,
        "isempty" => FilterOperator.ArrayIsEmpty,
        "notempty" => FilterOperator.ArrayNotEmpty,
        _ => throw new ArgumentException($"Operazione filtro sconosciuta: '{operation}'.")
    };

    public static List<SortField> BuildSort(List<SortingDto>? sortBy) =>
        sortBy?.Select(s => new SortField(s.Field, ParseDirection(s.Direction))).ToList() ?? [];

    private static SortDirection ParseDirection(string direction) => direction.Trim().ToLowerInvariant() switch
    {
        "desc" or "descending" => SortDirection.Descending,
        _ => SortDirection.Ascending
    };

    public static PageRequest BuildPage(OptionsDto? options) =>
        new(options?.Page ?? PageRequest.Default.Number, options?.PageSize ?? PageRequest.Default.Size);

    public static void AddDateRange(List<FilterNode> filters, string field, DateOnlyRangeDto? range)
    {
        if (range is null) return;
        if (range.From.HasValue)
            filters.Add(Filter.Gte(field, range.From.Value.ToDateTime(TimeOnly.MinValue)));
        if (range.To.HasValue)
            filters.Add(Filter.Lte(field, range.To.Value.ToDateTime(TimeOnly.MinValue)));
    }

    public static void AddIn(List<FilterNode> filters, string field, IEnumerable<object?>? values)
    {
        var list = values?.ToList();
        if (list is { Count: > 0 })
            filters.Add(Filter.In(field, list.ToArray()));
    }

}