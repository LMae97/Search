using System.Globalization;
using System.Text;
using WeByte.Search.Core.Filters;
using WeByte.Search.Core.Metadata;
using WeByte.Search.Export;

namespace WeByte.Search.Tests;

/// <summary>
/// Fedeltà al file prodotto dall'export legacy (separatore <c>;</c>, BOM, multivalore <c>::</c>,
/// a capo appiattiti) più le due cose che il legacy sbagliava: quoting e formattazione dipendente dalla
/// culture del server.
/// </summary>
public class CsvTabularWriterTests
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

    private static string Csv(
        IReadOnlyList<FieldDescriptor> columns,
        IEnumerable<IReadOnlyDictionary<string, object?>> rows,
        CsvExportOptions? options = null)
    {
        using var stream = new MemoryStream();
        CsvTabularWriter.Write(stream, columns, rows, options);
        // Decodifica esplicita senza rimuovere il BOM: alcuni test lo verificano.
        return new UTF8Encoding(false).GetString(stream.ToArray());
    }

    [Fact]
    public void Header_uses_labels_and_semicolon_with_crlf()
    {
        var csv = Csv(
            [Column("name", label: "Nome"), Column("email", label: "Email")],
            [Row(("name", "Acea"), ("email", "info@acea.it"))]);

        Assert.Equal("﻿Nome;Email\r\nAcea;info@acea.it\r\n", csv);
    }

    [Fact]
    public void Columns_drive_order_and_missing_keys_become_empty_cells()
    {
        // La riga ha le chiavi in ordine diverso e le manca "email": il file segue le colonne, non il dizionario.
        var csv = Csv(
            [Column("name"), Column("email"), Column("phone")],
            [Row(("phone", "123"), ("name", "Acea"))]);

        Assert.Equal("﻿name;email;phone\r\nAcea;;123\r\n", csv);
    }

    [Fact]
    public void Array_values_are_joined_in_one_cell_skipping_nulls()
    {
        var csv = Csv(
            [Column("tags", isArray: true)],
            [Row(("tags", new List<object?> { "alfa", null, "beta" }))]);

        Assert.Equal("﻿tags\r\nalfa::beta\r\n", csv);
    }

    [Fact]
    public void Link_fields_export_the_label_not_the_reference()
    {
        // Forma prodotta dal SELECT SQL: json_build_object('value', ..., 'label', ...).
        var link = new Dictionary<string, object?>
        {
            ["value"] = "/brands/1f47213e-7d08-48d1-8a85-466c8a51edc5",
            ["label"] = "Acea"
        };

        var csv = Csv([Column("brand", FieldKind.Link)], [Row(("brand", link))]);

        Assert.Equal("﻿brand\r\nAcea\r\n", csv);
    }

    [Fact]
    public void Link_without_label_falls_back_to_the_reference()
    {
        var link = new Dictionary<string, object?> { ["value"] = "/brands/42" };

        var csv = Csv([Column("brand", FieldKind.Link)], [Row(("brand", link))]);

        Assert.Equal("﻿brand\r\n/brands/42\r\n", csv);
    }

    [Fact]
    public void Values_containing_the_delimiter_or_quotes_are_quoted_and_escaped()
    {
        var csv = Csv(
            [Column("a"), Column("b")],
            [Row(("a", "Rossi; Bianchi"), ("b", "dice \"ciao\""))]);

        // Senza quoting il ';' avrebbe spostato tutte le colonne successive: è il bug del CSV legacy sincrono.
        Assert.Equal("﻿a;b\r\n\"Rossi; Bianchi\";\"dice \"\"ciao\"\"\"\r\n", csv);
    }

    [Fact]
    public void New_lines_inside_values_are_flattened_to_spaces_like_the_legacy()
    {
        var csv = Csv([Column("note")], [Row(("note", "prima\r\nseconda\nterza"))]);

        Assert.Equal("﻿note\r\nprima seconda terza\r\n", csv);
    }

    [Fact]
    public void New_lines_can_be_preserved_and_then_the_field_is_quoted()
    {
        var csv = Csv(
            [Column("note")],
            [Row(("note", "prima\nseconda"))],
            new CsvExportOptions { ReplaceNewLinesWithSpace = false });

        Assert.Equal("﻿note\r\n\"prima\nseconda\"\r\n", csv);
    }

    [Fact]
    public void Numbers_and_dates_are_culture_independent_by_default()
    {
        var columns = new[] { Column("price", FieldKind.Decimal), Column("createdAt", FieldKind.DateTime) };
        var row = Row(("price", 1234.56m), ("createdAt", new DateTime(2026, 7, 30, 14, 5, 9)));

        var csv = Csv(columns, [row]);

        Assert.Equal("﻿price;createdAt\r\n1234.56;2026-07-30 14:05:09\r\n", csv);
    }

    [Fact]
    public void Numbers_can_use_a_local_culture_for_excel()
    {
        var options = new CsvExportOptions
        {
            Values = new ExportValueFormat { FormatProvider = new CultureInfo("it-IT") }
        };

        var csv = Csv([Column("price", FieldKind.Decimal)], [Row(("price", 1234.56m))], options);

        // Virgola decimale: è così che un Excel italiano riconosce la cella come numero e non come testo.
        Assert.Equal("﻿price\r\n1234,56\r\n", csv);
    }

    [Fact]
    public void Date_formats_are_configurable_and_stay_literal_across_cultures()
    {
        var options = new CsvExportOptions
        {
            Values = new ExportValueFormat
            {
                // Barre quotate: restano letterali anche con una culture il cui separatore di data è altro.
                DateFormat = "dd'/'MM'/'yyyy",
                DateTimeFormat = "dd'/'MM'/'yyyy HH:mm",
                FormatProvider = new CultureInfo("de-DE")
            }
        };

        var columns = new[] { Column("createdAt", FieldKind.DateTime), Column("signedOn", FieldKind.DateTime) };
        var row = Row(
            ("createdAt", new DateTime(2024, 12, 7, 12, 29, 44)),
            ("signedOn", new DateOnly(2024, 12, 7)));

        var csv = Csv(columns, [row], options);

        Assert.Equal("﻿createdAt;signedOn\r\n07/12/2024 12:29;07/12/2024\r\n", csv);
    }

    [Fact]
    public void Italian_preset_formats_dates_and_decimals_for_excel()
    {
        var options = new CsvExportOptions { Values = ExportValueFormat.Italian };

        var columns = new[] { Column("createdAt", FieldKind.DateTime), Column("price", FieldKind.Decimal) };
        var row = Row(("createdAt", new DateTime(2024, 12, 7, 12, 29, 44)), ("price", 1234.56m));

        var csv = Csv(columns, [row], options);

        Assert.Equal("﻿createdAt;price\r\n07/12/2024 12:29;1234,56\r\n", csv);
    }

    [Fact]
    public void Bom_can_be_omitted_for_appended_batches()
    {
        var csv = Csv(
            [Column("name")],
            [Row(("name", "Acea"))],
            new CsvExportOptions { WriteByteOrderMark = false });

        Assert.Equal("name;Acea\r\n".Replace(";", "\r\n"), csv.Replace(";", "\r\n"));
        Assert.DoesNotContain('﻿', csv);
    }

    [Fact]
    public void Batched_writing_emits_the_header_once()
    {
        using var stream = new MemoryStream();

        // Primo batch: BOM + intestazione.
        using (var writer = new CsvTabularWriter(stream, [Column("name")]))
        {
            writer.WriteHeader();
            writer.WriteRows([Row(("name", "Acea"))]);
        }

        // Batch successivo: né BOM né intestazione, si accoda.
        using (var writer = new CsvTabularWriter(
            stream, [Column("name")], new CsvExportOptions { WriteByteOrderMark = false }))
        {
            writer.WriteRows([Row(("name", "Apple"))]);
        }

        var csv = new UTF8Encoding(false).GetString(stream.ToArray());

        Assert.Equal("﻿name\r\nAcea\r\nApple\r\n", csv);
    }
}
