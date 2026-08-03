namespace WeByte.Search.Application.Export;

/// <summary>
/// Esito di <see cref="SearchExporter.Decide"/>: se l'export può essere scritto subito nella richiesta
/// corrente, o se è troppo grande e va deferito a un processo esterno.
/// <para>
/// Un tipo esplicito al posto del sentinel del vecchio sistema (<c>ResultCount == -1000</c>): lì "troppo
/// grande" era indistinguibile da un dato qualunque se non per un numero magico da conoscere a memoria;
/// qui il compilatore obbliga a gestire entrambi i casi con un <c>switch</c> esaustivo.
/// </para>
/// </summary>
public abstract record ExportDecision
{
    private ExportDecision() { }

    /// <summary>L'export rientra nella soglia: puoi chiamare <see cref="SearchExporter.Write"/>.</summary>
    public sealed record Sync : ExportDecision;

    /// <summary>
    /// L'export supera la soglia sincrona.
    /// <para>
    /// <see cref="RowCount"/> NON è garantito il totale esatto: il conteggio si ferma appena supera
    /// <see cref="MaxSyncRows"/> (più un piccolo margine), quindi qui vale solo "almeno
    /// <see cref="MaxSyncRows"/> + 1" — sufficiente a giustificare il rifiuto, non a dire quanto sia grande
    /// davvero il risultato. <see cref="MaxSyncRows"/> è quante righe sarebbero state accettabili con la
    /// proiezione richiesta — utile per un messaggio d'errore o una diagnostica.
    /// </para>
    /// </summary>
    public sealed record TooLargeForSync(long RowCount, int MaxSyncRows) : ExportDecision;
}
