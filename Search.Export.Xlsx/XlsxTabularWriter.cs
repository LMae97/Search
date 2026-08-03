using System.Globalization;
using ClosedXML.Excel;
using WeByte.Search.Core.Metadata;
using WeByte.Search.Export;

namespace WeByte.Search.Export.Xlsx;

/// <summary>
/// Scrive righe di ricerca in un foglio Excel (.xlsx), con celle <b>tipizzate</b>: numeri sommabili, date
/// ordinabili, booleani localizzati da Excel.
/// <para>
/// <b>Differenza sostanziale dal CSV</b>: ClosedXML compone l'intero workbook in memoria e lo serializza
/// solo al salvataggio, quindi i batch riducono la memoria delle <i>query</i> ma non quella del foglio. Vale
/// anche per il legacy: la variante asincrona scriveva su file temporaneo, il che evitava una seconda copia
/// in un <c>MemoryStream</c> ma non abbassava il picco. Per volumi che non stanno in RAM il formato giusto
/// è il CSV, che è davvero in streaming.
/// </para>
/// </summary>
public sealed class XlsxTabularWriter : ITabularWriter
{
    private readonly Stream _destination;
    private readonly IReadOnlyList<FieldDescriptor> _columns;
    private readonly XlsxExportOptions _options;
    private readonly ExportValueFormatter _formatter;
    private readonly XLWorkbook _workbook;
    private readonly IXLWorksheet _sheet;

    // Formato data per colonna, dedotto dal primo valore incontrato: applicarlo a tutto l'intervallo alla
    // fine costa una sola operazione di stile, invece di una per cella (che su 10.000 righe si sente).
    private readonly string?[] _columnDateFormats;

    private int _lastRow;
    private bool _saved;

    public XlsxTabularWriter(
        Stream destination,
        IReadOnlyList<FieldDescriptor> columns,
        XlsxExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(columns);

        _destination = destination;
        _columns = columns;
        _options = options ?? XlsxExportOptions.Default;
        _formatter = new ExportValueFormatter(_options.Values);
        _columnDateFormats = new string?[columns.Count];

        _workbook = new XLWorkbook();
        _sheet = _workbook.Worksheets.Add(_options.WorksheetName);
    }

    public void WriteHeader()
    {
        var labels = HeaderLabels(_columns);

        for (var index = 0; index < labels.Count; index++)
            _sheet.Cell(1, index + 1).Value = labels[index];

        _lastRow = 1;
    }

    public void WriteRows(IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (var row in rows) WriteRow(row);
    }

    public void WriteRow(IReadOnlyDictionary<string, object?> row)
    {
        ArgumentNullException.ThrowIfNull(row);

        _lastRow++;

        for (var index = 0; index < _columns.Count; index++)
        {
            var column = _columns[index];
            var raw = row.TryGetValue(column.Name, out var found) ? found : null;

            _columnDateFormats[index] ??= DateFormatFor(raw);

            SetCell(_sheet.Cell(_lastRow, index + 1), _formatter.FormatCell(raw, column));
        }
    }

    /// <summary>Finalizza il foglio (formati, tabella, larghezze) e lo serializza nello stream.</summary>
    public void Dispose()
    {
        if (_saved)
        {
            _workbook.Dispose();
            return;
        }

        _saved = true;

        try
        {
            if (_columns.Count > 0)
            {
                ApplyDateFormats();

                _sheet.ColumnWidth = _options.ColumnWidth;
                _sheet.RowHeight = _options.RowHeight;

                if (_options.CreateTable)
                {
                    // L'intervallo include la riga di intestazione: è così che Excel sa quali sono i titoli.
                    var table = _sheet.Range(1, 1, Math.Max(_lastRow, 1), _columns.Count).CreateTable();
                    table.Theme = _options.TableTheme;
                }
            }

            _workbook.SaveAs(_destination);
        }
        finally
        {
            _workbook.Dispose();
        }
    }

    private void ApplyDateFormats()
    {
        if (_lastRow < 2) return; // nessuna riga di dati

        for (var index = 0; index < _columns.Count; index++)
        {
            var format = _columnDateFormats[index];
            if (format is null) continue;

            _sheet.Range(2, index + 1, _lastRow, index + 1).Style.NumberFormat.Format = format;
        }
    }

    private string? DateFormatFor(object? value) => value switch
    {
        DateOnly => _options.DateFormat,
        DateTime or DateTimeOffset => _options.DateTimeFormat,
        _ => null
    };

    // Si passa per i setter tipizzati invece di assegnare un object: in ClosedXML il valore di cella è una
    // union di tipi, ed è l'assegnazione tipizzata che fa capire a Excel se ha davanti un numero, una data,
    // un booleano o del testo. Gli interi diventano double perché Excel memorizza i numeri così comunque.
    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                return; // cella vuota
            case string text:
                cell.Value = text;
                return;
            case bool flag:
                cell.Value = flag;
                return;
            case DateTime instant:
                cell.Value = instant;
                return;
            case TimeSpan duration:
                cell.Value = duration;
                return;
            case decimal number:
                cell.Value = number;
                return;
            default:
                if (IsNumeric(value))
                {
                    cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    return;
                }

                cell.Value = value.ToString();
                return;
        }
    }

    private static bool IsNumeric(object value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double;

    // Le intestazioni di una tabella Excel devono essere univoche e non vuote, altrimenti ClosedXML rifiuta
    // di crearla. Due campi con la stessa Label sono plausibili (le etichette le scrive un umano a DB): il
    // legacy in quel caso moriva, perché usava le Label come nomi di colonna di un DataTable.
    private static IReadOnlyList<string> HeaderLabels(IReadOnlyList<FieldDescriptor> columns)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var labels = new List<string>(columns.Count);

        foreach (var column in columns)
        {
            var label = string.IsNullOrWhiteSpace(column.Label) ? column.Name : column.Label;

            var candidate = label;
            var suffix = 2;
            while (!used.Add(candidate)) candidate = $"{label} ({suffix++})";

            labels.Add(candidate);
        }

        return labels;
    }
}

/// <summary>Fabbrica dello scrittore xlsx, con le opzioni fissate una volta all'avvio.</summary>
public sealed class XlsxTabularWriterFactory : ITabularWriterFactory
{
    private readonly XlsxExportOptions _options;

    public XlsxTabularWriterFactory(XlsxExportOptions? options = null) => _options = options ?? XlsxExportOptions.Default;

    public string Format => "xlsx";

    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public string FileExtension => "xlsx";

    public ITabularWriter Create(Stream destination, IReadOnlyList<FieldDescriptor> columns) =>
        new XlsxTabularWriter(destination, columns, _options);
}
