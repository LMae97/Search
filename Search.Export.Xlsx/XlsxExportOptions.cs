using ClosedXML.Excel;
using WeByte.Search.Export;

namespace WeByte.Search.Export.Xlsx;

/// <summary>
/// Opzioni del formato xlsx. I default riproducono il foglio prodotto dall'export legacy: un unico
/// worksheet con una tabella Excel (quindi filtri e righe alternate), colonne larghe 24 e tema
/// <c>TableStyleLight16</c>.
/// </summary>
public sealed record XlsxExportOptions
{
    public string WorksheetName { get; init; } = "Export";

    public double ColumnWidth { get; init; } = 24;

    public double RowHeight { get; init; } = 15;

    /// <summary>
    /// Trasforma l'intervallo in una tabella Excel: intestazioni bloccate, filtri automatici, stile a bande.
    /// È ciò che faceva <c>tableRange.CreateTable()</c> nel legacy.
    /// </summary>
    public bool CreateTable { get; init; } = true;

    public XLTableTheme TableTheme { get; init; } = XLTableTheme.TableStyleLight16;

    /// <summary>
    /// Formato numerico applicato alle colonne data (sintassi Excel, non .NET).
    /// <para>
    /// Il legacy non lo impostava, quindi il foglio ereditava il formato di default della macchina che lo
    /// apriva: lo stesso export appariva diverso a utenti diversi, e in certi casi come numero seriale.
    /// Renderlo esplicito è una differenza deliberata. Per il gusto italiano: <c>dd/mm/yyyy</c>.
    /// </para>
    /// </summary>
    public string DateFormat { get; init; } = "yyyy-mm-dd";

    public string DateTimeFormat { get; init; } = "yyyy-mm-dd hh:mm:ss";

    public ExportValueFormat Values { get; init; } = ExportValueFormat.Default;

    public static XlsxExportOptions Default { get; } = new();

    /// <summary>
    /// Preset per un pubblico italiano: <c>dd/mm/yyyy</c> (sintassi Excel — <c>mm</c> è il mese fra parti di
    /// data, i minuti si scrivono <c>mm</c> anche loro ma dopo <c>hh:</c>, è la posizione a disambiguare).
    /// <see cref="Values"/> usa <see cref="ExportValueFormat.Italian"/>, per le date che finiscono in un
    /// array e restano testo invece che una cella tipizzata.
    /// </summary>
    public static XlsxExportOptions Italian { get; } = new()
    {
        Values = ExportValueFormat.Italian,
        DateFormat = "dd/mm/yyyy",
        DateTimeFormat = "dd/mm/yyyy hh:mm"
    };
}
