using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Search.Application.Querying.Metadata;

namespace Search.Application.Querying;

/// <summary>
/// Token opaco per keyset pagination: codifica i valori delle colonne di sort dell'ultimo record,
/// così la query successiva riparte da lì senza OFFSET.
/// Il formato interno (JSON → Base64Url) è un dettaglio: il client lo tratta come stringa opaca.
/// <para>
/// Usa <b>Base64Url</b> (non Base64 standard): niente <c>+</c>/<c>/</c> e niente padding <c>=</c>, così il
/// token è sicuro in URL/query-string e non si rompe se il padding viene perso lungo il tragitto.
/// </para>
/// </summary>
public static class SearchCursor
{
    public static string Encode(IReadOnlyList<SortField> sortFields, IReadOnlyDictionary<string, object?> lastItem)
    {
        var values = new List<object?>(sortFields.Count);
        foreach (var sort in sortFields)
            values.Add(lastItem.TryGetValue(sort.Field, out var v) ? v : null);

        var json = JsonSerializer.Serialize(values);
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Decodifica il cursor e converte ogni valore al ClrType del campo di sort corrispondente,
    /// così Npgsql riceve il tipo corretto (es. Guid, non string).
    /// </summary>
    public static IReadOnlyList<object?> Decode(string cursor, IEntitySearchMap map, IReadOnlyList<SortField> sortFields)
    {
        var json = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor));
        var elements = JsonSerializer.Deserialize<JsonElement[]>(json)
            ?? throw new InvalidOperationException("Cursor non valido.");

        if (elements.Length != sortFields.Count)
            throw new InvalidOperationException(
                $"Il cursor contiene {elements.Length} valori ma il sort ha {sortFields.Count} campi.");

        var values = new List<object?>(elements.Length);
        for (var i = 0; i < elements.Length; i++)
        {
            var targetType = map.TryGetField(sortFields[i].Field, out var field)
                ? field.ClrType
                : null;
            values.Add(ToClr(elements[i], targetType));
        }
        return values;
    }

    private static object? ToClr(JsonElement element, Type? targetType)
    {
        if (element.ValueKind == JsonValueKind.Null)
            return null;

        if (targetType is not null)
        {
            var raw = element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.GetRawText();

            if (targetType == typeof(Guid))
                return Guid.Parse(raw!);
            if (targetType == typeof(DateTime))
                return DateTime.Parse(raw!);
            if (targetType == typeof(DateTimeOffset))
                return DateTimeOffset.Parse(raw!);
            if (targetType == typeof(int))
                return element.GetInt32();
            if (targetType == typeof(long))
                return element.GetInt64();
            if (targetType == typeof(decimal))
                return element.GetDecimal();
            if (targetType == typeof(double))
                return element.GetDouble();
            if (targetType == typeof(bool))
                return element.GetBoolean();
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.GetRawText()
        };
    }
}
