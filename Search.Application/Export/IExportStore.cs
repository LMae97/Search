namespace WeByte.Search.Application.Export;

/// <summary>Riferimento a un file di export prodotto e archiviato.</summary>
/// <param name="FileName">Nome del file, senza percorso.</param>
/// <param name="Location">Dove si trova: un percorso su disco in locale, una URL/SAS in cloud.</param>
/// <param name="SizeInBytes">Dimensione del file scritto.</param>
public sealed record StoredExport(string FileName, string Location, long SizeInBytes);

/// <summary>
/// Destinazione dei file di export. Astratta perché è l'unica cosa che cambia fra ambienti: in locale una
/// cartella, in cloud un blob con una SAS a scadenza (quello che il legacy chiamava
/// <c>temporary-file-export</c>).
/// <para>
/// La scrittura è <b>invertita</b>: lo store crea il file e passa lo stream al chiamante, invece di ricevere
/// un array di byte già pronto. Così i batch dell'export finiscono direttamente sulla destinazione, senza
/// una copia intermedia in memoria — che per un export grosso è la differenza fra funzionare e non.
/// </para>
/// </summary>
public interface IExportStore
{
    StoredExport Save(string fileName, Action<Stream> write);
}

/// <summary>
/// Store su cartella locale, per lo sviluppo e i test manuali. L'equivalente in produzione è un blob store:
/// stessa interfaccia, <see cref="StoredExport.Location"/> diventa una URL firmata invece di un percorso.
/// </summary>
public sealed class LocalDirectoryExportStore : IExportStore
{
    private readonly string _directory;

    public LocalDirectoryExportStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public StoredExport Save(string fileName, Action<Stream> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(write);

        // Solo il nome: un fileName che contenesse un percorso non deve poter scrivere fuori dalla cartella.
        var safeName = Path.GetFileName(fileName);

        Directory.CreateDirectory(_directory);
        var fullPath = Path.Combine(_directory, safeName);

        using (var file = File.Create(fullPath))
            write(file);

        return new StoredExport(safeName, fullPath, new FileInfo(fullPath).Length);
    }
}
