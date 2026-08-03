namespace WeByte.Search.Export;

/// <summary>
/// Opzioni del formato CSV. I default riproducono il file prodotto dall'export legacy: separatore
/// <c>;</c> (Excel italiano lo apre in colonne senza wizard), UTF-8 con BOM (senza il BOM Excel
/// interpreta il file come ANSI e sbaglia gli accenti), CRLF.
/// </summary>
public sealed record CsvExportOptions
{
    public char Delimiter { get; init; } = ';';

    public string NewLine { get; init; } = "\r\n";

    /// <summary>
    /// Scrive il BOM UTF-8. In scrittura a batch (append su più chiamate) va messo a <c>false</c> da
    /// <b>tutti i batch tranne il primo</b>: il legacy non lo faceva e infilava un BOM in mezzo al file,
    /// visibile come <c>ï»¿</c> all'inizio di ogni blocco appeso.
    /// </summary>
    public bool WriteByteOrderMark { get; init; } = true;

    /// <summary>
    /// Sostituisce CR/LF dentro i valori con uno spazio, come il legacy. Con <c>false</c> gli a capo
    /// vengono preservati e il campo viene quotato (CSV valido, ma righe multilinea da rileggere).
    /// </summary>
    public bool ReplaceNewLinesWithSpace { get; init; } = true;

    public ExportValueFormat Values { get; init; } = ExportValueFormat.Default;

    public static CsvExportOptions Default { get; } = new();
}
