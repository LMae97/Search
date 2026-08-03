using WeByte.Search.Export;

namespace WeByte.Search.Api.Export;

/// <summary>
/// Registro dei formati di export disponibili, per chiave (<c>csv</c>, <c>xlsx</c>).
/// <para>
/// Sta nel progetto API perché è qui che si decide <b>quali</b> formati offrire: è l'unico progetto che
/// referenzia sia <c>SearchWithCount.Export</c> sia <c>SearchWithCount.Export.Xlsx</c>. Aggiungere un formato = registrare
/// una fabbrica nella DI, senza toccare né i controller né il layer applicativo.
/// </para>
/// </summary>
public sealed class ExportFormats
{
    private readonly IReadOnlyDictionary<string, ITabularWriterFactory> _factories;

    public ExportFormats(IEnumerable<ITabularWriterFactory> factories)
    {
        _factories = factories.ToDictionary(factory => factory.Format, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Elenco leggibile dei formati registrati, per i messaggi d'errore.</summary>
    public string Available => string.Join(", ", _factories.Keys.OrderBy(key => key));

    public bool TryGet(string? format, out ITabularWriterFactory factory) =>
        _factories.TryGetValue(format ?? string.Empty, out factory!);
}
