namespace WeByte.Search.Export;

/// <summary>
/// Limite di righe di un export, eseguito in un'<b>unica query</b> — requisito di progetto: paginare a
/// offset in più round-trip separati non è coerente su una tabella che può cambiare fra una query e
/// l'altra (righe inserite/cancellate spostano cosa si trova "all'offset N", causando duplicati o righe
/// saltate fra un batch e il successivo). Una singola query è una lettura sola, coerente per costruzione.
/// </summary>
public sealed record ExportPagingOptions
{
    /// <summary>Righe massime da esportare; <c>null</c> = tutte quelle che il filtro seleziona.</summary>
    public int? MaxRows { get; init; }

    public static ExportPagingOptions Default { get; } = new();
}
