using ClosedXML.Excel;
using WeByte.Search.Core.Filters;
using WeByte.Search.Core.Metadata;
using WeByte.Search.Export.Xlsx;

namespace WeByte.Search.Tests;

/// <summary>
/// L'xlsx si verifica rileggendo il file prodotto: la cosa che conta e che il CSV non può dare sono le celle
/// <b>tipizzate</b> — un numero deve essere un numero, non testo che somiglia a un numero.
/// </summary>
public class XlsxTabularWriterTests
{
    private static FieldDescriptor Column(
        string name, FieldKind kind = FieldKind.String, string? label = null, bool isArray = false)
        => FieldDescriptor.BuildPathBased(
            storagePath: name,
            name: name,
            responseId: name,
            kind: kind,
            isArray: isArray,
            clrType: typeof(string),
            jsonColumn: false,
            linkReferencePath: null,
            linkReferenceEntityId: null,
            label: label ?? name,
            section: null,
            defaultOrder: null,
            isHidden: false,
            requiredPermissionId: null,
            allowedOperators: new HashSet<FilterOperator>());

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] cells)
        => cells.ToDictionary(cell => cell.Key, cell => cell.Value);

    /// <summary>Scrive e rilegge, restituendo il foglio così come lo vedrebbe Excel.</summary>
    private static IXLWorksheet Sheet(
        IReadOnlyList<FieldDescriptor> columns,
        IEnumerable<IReadOnlyDictionary<string, object?>> rows,
        XlsxExportOptions? options = null)
    {
        var stream = new MemoryStream();

        using (var writer = new XlsxTabularWriter(stream, columns, options))
        {
            writer.WriteHeader();
            writer.WriteRows(rows);
        }

        stream.Position = 0;
        // Il workbook resta aperto per la durata del test: lo chiude il GC, non serve deterministico qui.
        return new XLWorkbook(stream).Worksheet(1);
    }

    [Fact]
    public void Header_uses_labels_on_the_first_row()
    {
        var sheet = Sheet(
            [Column("name", label: "Nome"), Column("email", label: "Email")],
            [Row(("name", "Acea"), ("email", "info@acea.it"))]);

        Assert.Equal("Nome", sheet.Cell(1, 1).GetString());
        Assert.Equal("Email", sheet.Cell(1, 2).GetString());
        Assert.Equal("Acea", sheet.Cell(2, 1).GetString());
    }

    [Fact]
    public void Numbers_are_written_as_numbers_not_text()
    {
        var sheet = Sheet([Column("price", FieldKind.Decimal)], [Row(("price", 1234.56m))]);

        var cell = sheet.Cell(2, 1);

        Assert.Equal(XLDataType.Number, cell.DataType);
        Assert.Equal(1234.56, cell.GetDouble(), precision: 6);
    }

    [Fact]
    public void Integers_are_numbers_too()
    {
        var sheet = Sheet([Column("stock", FieldKind.Integer)], [Row(("stock", 42L))]);

        Assert.Equal(XLDataType.Number, sheet.Cell(2, 1).DataType);
        Assert.Equal(42, sheet.Cell(2, 1).GetDouble());
    }

    [Fact]
    public void Booleans_stay_booleans_so_excel_localises_them()
    {
        var sheet = Sheet([Column("isActive", FieldKind.Boolean)], [Row(("isActive", true))]);

        // Cella booleana: è Excel a mostrare VERO/FALSO secondo la lingua, non noi a scriverlo.
        Assert.Equal(XLDataType.Boolean, sheet.Cell(2, 1).DataType);
        Assert.True(sheet.Cell(2, 1).GetBoolean());
    }

    [Fact]
    public void Date_times_are_dates_with_an_explicit_number_format()
    {
        var sheet = Sheet(
            [Column("createdAt", FieldKind.DateTime)],
            [Row(("createdAt", new DateTime(2026, 7, 30, 14, 5, 9)))]);

        var cell = sheet.Cell(2, 1);

        Assert.Equal(XLDataType.DateTime, cell.DataType);
        Assert.Equal(new DateTime(2026, 7, 30, 14, 5, 9), cell.GetDateTime());
        Assert.Equal("yyyy-mm-dd hh:mm:ss", cell.Style.NumberFormat.Format);
    }

    [Fact]
    public void Date_only_values_get_the_date_format_and_midnight()
    {
        var sheet = Sheet(
            [Column("signedOn", FieldKind.DateTime)],
            [Row(("signedOn", new DateOnly(2026, 7, 30)))]);

        var cell = sheet.Cell(2, 1);

        Assert.Equal(XLDataType.DateTime, cell.DataType);
        Assert.Equal(new DateTime(2026, 7, 30), cell.GetDateTime());
        Assert.Equal("yyyy-mm-dd", cell.Style.NumberFormat.Format);
    }

    [Fact]
    public void Arrays_collapse_into_one_text_cell_like_the_csv()
    {
        var sheet = Sheet(
            [Column("tags", isArray: true)],
            [Row(("tags", new List<object?> { "alfa", null, "beta" }))]);

        Assert.Equal("alfa::beta", sheet.Cell(2, 1).GetString());
    }

    [Fact]
    public void Link_fields_export_the_label()
    {
        var link = new Dictionary<string, object?> { ["value"] = "/brands/42", ["label"] = "Acea" };

        var sheet = Sheet([Column("brand", FieldKind.Link)], [Row(("brand", link))]);

        Assert.Equal("Acea", sheet.Cell(2, 1).GetString());
    }

    [Fact]
    public void Missing_keys_and_nulls_leave_the_cell_empty()
    {
        var sheet = Sheet(
            [Column("name"), Column("email"), Column("phone")],
            [Row(("name", "Acea"), ("email", null))]);

        Assert.True(sheet.Cell(2, 2).IsEmpty());
        Assert.True(sheet.Cell(2, 3).IsEmpty());
    }

    [Fact]
    public void Columns_drive_order_not_the_dictionary()
    {
        var sheet = Sheet(
            [Column("name"), Column("phone")],
            [Row(("phone", "123"), ("name", "Acea"))]);

        Assert.Equal("Acea", sheet.Cell(2, 1).GetString());
        Assert.Equal("123", sheet.Cell(2, 2).GetString());
    }

    [Fact]
    public void The_range_becomes_an_excel_table_with_the_configured_theme()
    {
        var sheet = Sheet([Column("name")], [Row(("name", "Acea")), Row(("name", "Apple"))]);

        var table = Assert.Single(sheet.Tables);
        Assert.Equal(XLTableTheme.TableStyleLight16, table.Theme);
        Assert.Equal(2, table.DataRange.RowCount()); // intestazione esclusa
    }

    [Fact]
    public void Duplicate_labels_are_made_unique_instead_of_crashing()
    {
        // Due campi con la stessa etichetta: il legacy moriva qui (nomi di colonna duplicati nel DataTable),
        // e ClosedXML rifiuta di creare una tabella con intestazioni ripetute.
        var sheet = Sheet(
            [Column("brandName", label: "Segmento"), Column("segmentName", label: "Segmento")],
            [Row(("brandName", "Acea"), ("segmentName", "Energia"))]);

        Assert.Equal("Segmento", sheet.Cell(1, 1).GetString());
        Assert.Equal("Segmento (2)", sheet.Cell(1, 2).GetString());
    }

    [Fact]
    public void No_rows_still_produces_a_readable_sheet_with_the_header()
    {
        var sheet = Sheet([Column("name", label: "Nome")], []);

        Assert.Equal("Nome", sheet.Cell(1, 1).GetString());
        Assert.True(sheet.Cell(2, 1).IsEmpty());
    }

    [Fact]
    public void Date_number_formats_are_configurable_for_an_italian_audience()
    {
        var options = new XlsxExportOptions
        {
            DateFormat = "dd/mm/yyyy",
            DateTimeFormat = "dd/mm/yyyy hh:mm"
        };

        var sheet = Sheet(
            [Column("createdAt", FieldKind.DateTime), Column("signedOn", FieldKind.DateTime)],
            [Row(("createdAt", new DateTime(2024, 12, 7, 12, 29, 44)), ("signedOn", new DateOnly(2024, 12, 7)))],
            options);

        // Il valore resta una data vera (ordinabile, filtrabile): cambia solo come Excel la mostra.
        Assert.Equal(XLDataType.DateTime, sheet.Cell(2, 1).DataType);
        Assert.Equal("dd/mm/yyyy hh:mm", sheet.Cell(2, 1).Style.NumberFormat.Format);
        Assert.Equal("dd/mm/yyyy", sheet.Cell(2, 2).Style.NumberFormat.Format);
    }

    [Fact]
    public void Italian_preset_formats_dates_and_decimals_for_excel()
    {
        var sheet = Sheet(
            [Column("createdAt", FieldKind.DateTime), Column("price", FieldKind.Decimal)],
            [Row(("createdAt", new DateTime(2024, 12, 7, 12, 29, 44)), ("price", 1234.56m))],
            XlsxExportOptions.Italian);

        Assert.Equal("dd/mm/yyyy hh:mm", sheet.Cell(2, 1).Style.NumberFormat.Format);
        Assert.Equal(new DateTime(2024, 12, 7, 12, 29, 44), sheet.Cell(2, 1).GetDateTime());
        // Il numero resta un vero XLDataType.Number: la culture it-IT di Values incide solo sul CSV, dove
        // il valore diventa testo. In un xlsx la cella numerica non passa mai per una stringa formattata.
        Assert.Equal(XLDataType.Number, sheet.Cell(2, 2).DataType);
        Assert.Equal(1234.56, sheet.Cell(2, 2).GetDouble(), precision: 6);
    }

    [Fact]
    public void Worksheet_name_and_sizes_come_from_the_options()
    {
        var options = new XlsxExportOptions { WorksheetName = "Profili", ColumnWidth = 30, RowHeight = 18 };

        var sheet = Sheet([Column("name")], [Row(("name", "Acea"))], options);

        Assert.Equal("Profili", sheet.Name);
        Assert.Equal(30, sheet.ColumnWidth, precision: 3);
        Assert.Equal(18, sheet.RowHeight, precision: 3);
    }
}
