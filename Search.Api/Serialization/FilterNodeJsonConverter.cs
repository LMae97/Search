using System.Text.Json;
using System.Text.Json.Serialization;
using Search.Application.Querying.Filters;

namespace Search.Api.Serialization;

/// <summary>
/// Deserializza l'albero di filtri dal JSON del FE. Contratto:
/// <code>
/// { "and": [ ... ] } | { "or": [ ... ] } | { "not": { ... } }
/// { "field": "price", "op": "gte", "value": 10 }
/// { "field": "tags", "op": "containsAny", "values": ["a","b"] }
/// </code>
/// Gli operatori accettano alias amichevoli (eq, gte, containsAny…) o il nome dell'enum.
/// È di sola lettura: l'albero non viene mai serializzato verso il FE.
/// </summary>
public sealed class FilterNodeJsonConverter : JsonConverter<FilterNode>
{
    private static readonly Dictionary<string, FilterOperator> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eq"] = FilterOperator.Equals,
        ["ne"] = FilterOperator.NotEquals,
        ["gt"] = FilterOperator.GreaterThan,
        ["gte"] = FilterOperator.GreaterThanOrEqual,
        ["lt"] = FilterOperator.LessThan,
        ["lte"] = FilterOperator.LessThanOrEqual,
        ["between"] = FilterOperator.Between,
        ["in"] = FilterOperator.In,
        ["notIn"] = FilterOperator.NotIn,
        ["contains"] = FilterOperator.Contains,
        ["notContains"] = FilterOperator.NotContains,
        ["startsWith"] = FilterOperator.StartsWith,
        ["endsWith"] = FilterOperator.EndsWith,
        ["isNull"] = FilterOperator.IsNull,
        ["isNotNull"] = FilterOperator.IsNotNull,
        ["arrayContains"] = FilterOperator.ArrayContains,
        ["containsAny"] = FilterOperator.ArrayContainsAny,
        ["containsAll"] = FilterOperator.ArrayContainsAll,
        ["isEmpty"] = FilterOperator.ArrayIsEmpty,
        ["notEmpty"] = FilterOperator.ArrayNotEmpty
    };

    public override FilterNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return Parse(document.RootElement);
    }

    private static FilterNode Parse(JsonElement element)
    {
        if (element.TryGetProperty("and", out var and))
            return LogicalFilterNode.And(ParseChildren(and));
        if (element.TryGetProperty("or", out var or))
            return LogicalFilterNode.Or(ParseChildren(or));
        if (element.TryGetProperty("not", out var not))
            return LogicalFilterNode.Not(Parse(not));

        if (element.TryGetProperty("field", out var fieldElement))
        {
            var field = fieldElement.GetString()
                ?? throw new JsonException("'field' deve essere una stringa.");
            var op = ParseOperator(GetRequiredString(element, "op"));
            return new ComparisonFilterNode(field, op, ReadValues(element));
        }

        throw new JsonException("Nodo di filtro non riconosciuto: atteso 'and'/'or'/'not' oppure 'field'+'op'.");
    }

    private static FilterNode[] ParseChildren(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
            throw new JsonException("'and'/'or' devono essere array.");
        return array.EnumerateArray().Select(Parse).ToArray();
    }

    private static object?[] ReadValues(JsonElement element)
    {
        if (element.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array)
            return values.EnumerateArray().Select(ReadScalar).ToArray();
        if (element.TryGetProperty("value", out var value) && value.ValueKind != JsonValueKind.Null)
            return [ReadScalar(value)];
        return [];
    }

    private static object? ReadScalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => throw new JsonException($"Valore di filtro non supportato: {element.ValueKind}.")
    };

    private static FilterOperator ParseOperator(string op)
    {
        if (Aliases.TryGetValue(op, out var aliased))
            return aliased;
        if (Enum.TryParse<FilterOperator>(op, ignoreCase: true, out var parsed))
            return parsed;
        throw new JsonException($"Operatore non valido: '{op}'.");
    }

    private static string GetRequiredString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new JsonException($"Proprietà '{property}' mancante o non stringa.");

    public override void Write(Utf8JsonWriter writer, FilterNode value, JsonSerializerOptions options)
        => throw new NotSupportedException("La serializzazione dei filtri non è supportata (solo lettura dal FE).");
}
