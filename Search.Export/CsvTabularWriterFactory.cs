using WeByte.Search.Core.Metadata;

namespace WeByte.Search.Export;

/// <summary>Fabbrica dello scrittore CSV, con le opzioni fissate una volta all'avvio.</summary>
public sealed class CsvTabularWriterFactory : ITabularWriterFactory
{
    private readonly CsvExportOptions _options;

    public CsvTabularWriterFactory(CsvExportOptions? options = null) => _options = options ?? CsvExportOptions.Default;

    public string Format => "csv";

    public string ContentType => "text/csv";

    public string FileExtension => "csv";

    public ITabularWriter Create(Stream destination, IReadOnlyList<FieldDescriptor> columns) =>
        new CsvTabularWriter(destination, columns, _options);
}
