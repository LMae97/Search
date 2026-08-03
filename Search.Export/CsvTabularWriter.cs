using System.Text;
using WeByte.Search.Core.Metadata;

namespace WeByte.Search.Export;

/// <summary>
/// Scrive righe di ricerca in CSV su uno <see cref="Stream"/>.
/// <list type="bullet">
/// <item><b>Le colonne comandano</b>: numero, ordine ed etichette vengono da <c>columns</c>; ogni riga è
/// una lookup per <see cref="FieldDescriptor.Name"/>. Chiave assente ⇒ cella vuota (nessuna eccezione:
/// una proiezione può legittimamente non produrre una chiave per una riga).</item>
/// <item><b>Scrive e dimentica</b>: consuma le righe in streaming, senza materializzarle. È questo, non
/// una <c>GC.Collect()</c>, che tiene bassa la memoria su export grossi.</item>
/// <item><b>Non decide cosa esportare</b>: filtrare i campi nascosti (<see cref="FieldDescriptor.IsHidden"/>)
/// è del chiamante — <c>columns.Where(c => !c.IsHidden)</c> — perché "cosa può vedere l'utente" è
/// autorizzazione, non formattazione.</item>
/// </list>
/// Dove finisce lo stream (blob, disco, risposta HTTP) non è affare di questa classe.
/// </summary>
public sealed class CsvTabularWriter : ITabularWriter
{
    private readonly IReadOnlyList<FieldDescriptor> _columns;
    private readonly CsvExportOptions _options;
    private readonly ExportValueFormatter _formatter;
    private readonly TextWriter _writer;

    /// <param name="leaveOpen">
    /// Default <c>true</c>: lo stream è del chiamante (tipicamente da caricare/inviare dopo la scrittura),
    /// quindi chiudere il writer non deve chiuderlo.
    /// </param>
    public CsvTabularWriter(
        Stream stream,
        IReadOnlyList<FieldDescriptor> columns,
        CsvExportOptions? options = null,
        bool leaveOpen = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(columns);

        _columns = columns;
        _options = options ?? CsvExportOptions.Default;
        _formatter = new ExportValueFormatter(_options.Values);
        _writer = new StreamWriter(stream, new UTF8Encoding(_options.WriteByteOrderMark), leaveOpen: leaveOpen);
    }

    /// <summary>Riga di intestazione con le <see cref="FieldDescriptor.Label"/> delle colonne.</summary>
    public void WriteHeader() => WriteFields(_columns.Select(column => column.Label));

    public void WriteRows(IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (var row in rows) WriteRow(row);
    }

    public void WriteRow(IReadOnlyDictionary<string, object?> row)
    {
        ArgumentNullException.ThrowIfNull(row);
        WriteFields(_columns.Select(column => Cell(row, column)));
    }

    public void Flush() => _writer.Flush();

    public void Dispose()
    {
        _writer.Flush();
        _writer.Dispose();
    }

    /// <summary>Export in un colpo solo (intestazione + righe) quando i dati stanno in un'unica pagina.</summary>
    public static void Write(
        Stream stream,
        IReadOnlyList<FieldDescriptor> columns,
        IEnumerable<IReadOnlyDictionary<string, object?>> rows,
        CsvExportOptions? options = null)
    {
        using var writer = new CsvTabularWriter(stream, columns, options);
        writer.WriteHeader();
        writer.WriteRows(rows);
    }

    private string Cell(IReadOnlyDictionary<string, object?> row, FieldDescriptor column)
    {
        var value = row.TryGetValue(column.Name, out var found) ? found : null;
        var text = _formatter.Format(value, column);

        return _options.ReplaceNewLinesWithSpace
            ? text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ')
            : text;
    }

    private void WriteFields(IEnumerable<string> fields)
    {
        var first = true;
        foreach (var field in fields)
        {
            if (!first) _writer.Write(_options.Delimiter);
            WriteField(field);
            first = false;
        }

        _writer.Write(_options.NewLine);
    }

    // Quoting RFC 4180: si quota solo quando serve e le virgolette interne si raddoppiano. Il legacy CSV
    // sincrono concatenava i valori a mano senza quoting: un valore con un ';' spostava tutte le colonne
    // successive di quella riga, corrompendo il file in silenzio.
    private void WriteField(string value)
    {
        if (!NeedsQuoting(value))
        {
            _writer.Write(value);
            return;
        }

        _writer.Write('"');
        _writer.Write(value.Replace("\"", "\"\""));
        _writer.Write('"');
    }

    private bool NeedsQuoting(string value) =>
        value.Length > 0
        && (value.Contains(_options.Delimiter)
            || value.Contains('"')
            || value.Contains('\n')
            || value.Contains('\r')
            // Gli spazi ai bordi si perdono in rilettura se non quotati (stessa regola di CsvHelper).
            || char.IsWhiteSpace(value[0])
            || char.IsWhiteSpace(value[^1]));
}
