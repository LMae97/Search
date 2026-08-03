using WeByte.Search.Application.Querying;

namespace WeByte.Search.Application.Export;

/// <summary>
/// Soglia oltre la quale un export non va più scritto sincronamente nella richiesta corrente, ma deferito
/// a un processo esterno (coda + funzione, come nel vecchio sistema). <see cref="SearchExporter.Write"/>
/// esegue sempre l'export in un'unica query, quindi questa soglia è anche il limite oltre il quale quella
/// query sarebbe troppo grande per una richiesta HTTP sincrona.
/// <para>
/// Budget in <b>celle</b>, non in righe: il costo di generare l'export dipende da righe × colonne, quindi
/// una soglia in righe sarebbe troppo permissiva per una proiezione larga e inutilmente restrittiva per una
/// stretta.
/// </para>
/// <para>
/// Sta nel layer applicativo, non in <c>SearchWithCount.Export</c>: "quando smettere di essere sincroni" è una
/// policy, non una regola del motore di scrittura — che infatti non sa nulla di sincrono/asincrono.
/// </para>
/// </summary>
public sealed record ExportSizeLimit
{
    public int MaxSyncCells { get; init; } = 200_000;

    /// <summary>Righe massime esportabili in sincrono con questo numero di colonne.</summary>
    public int MaxSyncRows(int columnCount) => MaxSyncCells / Math.Max(1, columnCount);

    public bool ExceedsSync(long rowCount, int columnCount) => rowCount > MaxSyncRows(columnCount);

    public static ExportSizeLimit Default { get; } = new();
}
