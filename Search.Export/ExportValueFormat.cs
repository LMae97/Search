using System.Globalization;

namespace WeByte.Search.Export;

/// <summary>
/// Regole di conversione valore → <b>cella</b> di un file tabellare. Sono condivise da tutti i formati
/// (CSV oggi, xlsx domani): un file non ha tipi ricchi come una risposta JSON, quindi array, oggetti
/// <see cref="Core.Metadata.FieldKind.Link"/>, date e numeri vanno appiattiti in un testo — e la decisione
/// su <i>come</i> appiattirli è di presentazione, non del motore di ricerca.
/// <para>
/// I default riproducono il comportamento dell'export legacy (separatore <c>::</c> per i multivalore,
/// booleani <c>True</c>/<c>False</c>) tranne dove il legacy dipendeva dalla culture del server: qui
/// numeri e date sono <b>invarianti</b> per impostazione predefinita, così lo stesso export produce lo
/// stesso file su qualunque macchina.
/// </para>
/// </summary>
public sealed record ExportValueFormat
{
    /// <summary>Separatore fra gli elementi di un campo array dentro un'unica cella.</summary>
    public string MultiValueSeparator { get; init; } = "::";

    /// <summary>
    /// Culture per numeri e date. Invariante = punto decimale (<c>1234.56</c>). Per un Excel italiano che
    /// deve riconoscere i numeri come numeri, passare <c>new CultureInfo("it-IT")</c> (virgola decimale).
    /// </summary>
    public IFormatProvider FormatProvider { get; init; } = CultureInfo.InvariantCulture;

    /// <summary>Formato delle date senza ora (<see cref="DateOnly"/>).</summary>
    public string DateFormat { get; init; } = "yyyy-MM-dd";

    /// <summary>Formato di data e ora (<see cref="DateTime"/>, <see cref="DateTimeOffset"/>).</summary>
    public string DateTimeFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";

    public string BooleanTrue { get; init; } = "True";

    public string BooleanFalse { get; init; } = "False";

    /// <summary>
    /// Chiavi dell'oggetto prodotto dai campi <see cref="Core.Metadata.FieldKind.Link"/>
    /// (<c>{ value, label }</c>): nell'export vince l'etichetta, il riferimento serve solo alla UI per
    /// navigare. Se l'etichetta manca si ricade sul valore, per non produrre una cella vuota.
    /// </summary>
    public string LinkLabelKey { get; init; } = "label";

    public string LinkValueKey { get; init; } = "value";

    public static ExportValueFormat Default { get; } = new();

    /// <summary>
    /// Preset per un pubblico italiano: <c>dd/MM/yyyy</c>, culture <c>it-IT</c> (virgola decimale, così
    /// Excel riconosce i numeri come tali). Le barre sono quotate perché restino letterali qualunque sia la
    /// culture — <c>it-IT</c> le usa già come separatore, ma un domani potrebbe non essere la culture attiva
    /// del processo, e il formato non deve dipenderne.
    /// </summary>
    public static ExportValueFormat Italian { get; } = new()
    {
        FormatProvider = new CultureInfo("it-IT"),
        DateFormat = "dd'/'MM'/'yyyy",
        DateTimeFormat = "dd'/'MM'/'yyyy HH:mm"
    };
}
