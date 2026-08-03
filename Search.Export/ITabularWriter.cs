using WeByte.Search.Core.Metadata;

namespace WeByte.Search.Export;

/// <summary>
/// Scrittore di un file tabellare: riceve colonne e righe, non sa da dove vengono né dove finisce il file.
/// <para>
/// Il protocollo è <c>WriteHeader()</c> una volta, poi <c>WriteRows(...)</c> quante volte serve (un batch per
/// chiamata), poi <c>Dispose()</c> che finalizza e scarica. I formati differiscono su <i>quando</i> possono
/// scrivere davvero: il CSV emette subito, l'xlsx compone il foglio e lo serializza alla chiusura.
/// </para>
/// </summary>
public interface ITabularWriter : IDisposable
{
    void WriteHeader();

    void WriteRows(IEnumerable<IReadOnlyDictionary<string, object?>> rows);
}

/// <summary>
/// Costruisce lo scrittore di un formato. Esiste perché il layer applicativo possa esportare in un formato
/// qualsiasi <b>senza referenziarne le librerie</b>: chi vuole solo il CSV non si porta dietro ClosedXML.
/// Le implementazioni si registrano nella DI e si scelgono per <see cref="Format"/>.
/// </summary>
public interface ITabularWriterFactory
{
    /// <summary>Chiave del formato, in minuscolo: <c>csv</c>, <c>xlsx</c>.</summary>
    string Format { get; }

    /// <summary>MIME type da mettere nella risposta HTTP.</summary>
    string ContentType { get; }

    /// <summary>Estensione senza punto, per comporre il nome del file.</summary>
    string FileExtension { get; }

    ITabularWriter Create(Stream destination, IReadOnlyList<FieldDescriptor> columns);
}
