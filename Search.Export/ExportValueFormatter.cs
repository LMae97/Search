using System.Collections;
using System.Text.Json;
using WeByte.Search.Core.Metadata;

namespace WeByte.Search.Export;

/// <summary>
/// Converte il valore di una cella (così come esce dagli executor: scalare, lista, o oggetto annidato)
/// nel testo da scrivere nel file, guidato dai metadati del campo.
/// <para>
/// Punto unico, deliberatamente: nell'export legacy la stessa logica era duplicata in <c>GetValueOrNull</c>
/// (xlsx) e <c>PrintValue</c> (csv), che nel tempo hanno divergito — csv e xlsx dello stesso dato non
/// dicevano la stessa cosa.
/// </para>
/// </summary>
public sealed class ExportValueFormatter
{
    private readonly ExportValueFormat _format;

    public ExportValueFormatter(ExportValueFormat? format = null) => _format = format ?? ExportValueFormat.Default;

    /// <summary>Testo della cella per <paramref name="value"/> letto dal campo <paramref name="field"/>.</summary>
    public string Format(object? value, FieldDescriptor field)
    {
        if (value is null) return string.Empty;

        Func<object?, string> formatOne = field.Kind == FieldKind.Link ? FormatLink : FormatScalar;

        // Un campo array occupa comunque UNA cella: gli elementi si concatenano (i null si scartano, come
        // nel legacy, per non produrre separatori orfani tipo "a::::b").
        if (!IsMultiValue(value)) return formatOne(value);

        var parts = ((IEnumerable)value)
            .Cast<object?>()
            .Where(item => item is not null)
            .Select(formatOne);

        return string.Join(_format.MultiValueSeparator, parts);
    }

    /// <summary>
    /// Valore <b>tipizzato</b> della cella, per i formati che hanno tipi veri (xlsx): numeri come numeri,
    /// date come date, booleani come booleani. È ciò che distingue un foglio Excel usabile da uno in cui
    /// tutto è testo e non si può né sommare né ordinare.
    /// <para>
    /// Le regole su array e <see cref="FieldKind.Link"/> sono le stesse di <see cref="Format"/> — un array
    /// resta una cella di testo — così CSV e xlsx mostrano lo stesso contenuto per gli stessi dati.
    /// </para>
    /// </summary>
    public object? FormatCell(object? value, FieldDescriptor field)
    {
        if (value is null) return null;

        // Un array occupa una cella sola: diventa testo concatenato, esattamente come nel CSV.
        if (IsMultiValue(value)) return Format(value, field);

        if (field.Kind == FieldKind.Link) return FormatLink(value);

        return TypedScalar(value);
    }

    private object? TypedScalar(object? value) => value switch
    {
        null => null,
        string text => text,

        // Restituito come bool: Excel lo mostra già localizzato (VERO/FALSO su un Excel italiano). È il
        // motivo per cui il legacy doveva convertire a mano "VERO"/"FALSO" solo per gli array di booleani,
        // che diventando testo perdevano quella localizzazione automatica.
        bool flag => flag,

        // Excel non ha un tipo "solo data": si porta a mezzanotte, come faceva GetValueOrNull.
        DateOnly date => date.ToDateTime(TimeOnly.MinValue),
        DateTime instant => instant,
        DateTimeOffset instant => instant.DateTime,
        TimeOnly time => time.ToTimeSpan(),

        _ when IsObjectLike(value) => JsonSerializer.Serialize(value),
        _ when IsNumeric(value) => value,

        // Guid e affini: nessun tipo nativo nel foglio, quindi testo.
        _ => value.ToString()
    };

    private static bool IsNumeric(object value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    // string è IEnumerable<char> e un oggetto annidato (dizionario) è IEnumerable di coppie: nessuno dei due
    // è un "multivalore" da concatenare.
    private static bool IsMultiValue(object value) =>
        value is IEnumerable and not string && !IsObjectLike(value);

    private static bool IsObjectLike(object value) =>
        value is IDictionary || value is IReadOnlyDictionary<string, object?>;

    private string FormatLink(object? value)
    {
        if (value is null) return string.Empty;

        var label = Lookup(value, _format.LinkLabelKey) ?? Lookup(value, _format.LinkValueKey);

        // Non è un oggetto { value, label }: il campo è dichiarato Link ma lo store ha restituito uno
        // scalare (es. solo l'etichetta). Meglio scriverlo che perderlo.
        return label is null ? FormatScalar(value) : FormatScalar(label);
    }

    private static object? Lookup(object value, string key) => value switch
    {
        IReadOnlyDictionary<string, object?> map => map.TryGetValue(key, out var found) ? found : null,
        IDictionary map => map[key],
        _ => null
    };

    private string FormatScalar(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        bool flag => flag ? _format.BooleanTrue : _format.BooleanFalse,
        DateOnly date => date.ToString(_format.DateFormat, _format.FormatProvider),
        DateTime instant => instant.ToString(_format.DateTimeFormat, _format.FormatProvider),
        DateTimeOffset instant => instant.ToString(_format.DateTimeFormat, _format.FormatProvider),

        // Oggetto annidato su un campo NON Link (es. una colonna jsonb di forma libera): non esiste una
        // proiezione sensata in una cella, quindi si scrive il JSON invece del nome del tipo CLR.
        _ when IsObjectLike(value) => JsonSerializer.Serialize(value),

        IFormattable formattable => formattable.ToString(null, _format.FormatProvider),
        _ => value.ToString() ?? string.Empty
    };
}
