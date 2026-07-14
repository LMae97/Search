namespace Search.Infrastructure.Sql;

/// <summary>
/// Configurazione SQL di un'entità per lo store "raw" (<see cref="Dynamic.StoreKind.PostgresRaw"/>): come si
/// costruisce la query attorno ai campi. Vive nell'adapter SQL — <b>non</b> nei metadati store-agnostic
/// (<c>SearchEntity</c>/<c>FieldDescriptor</c>) — perché è dialetto/forma relazionale, esattamente come il
/// fluent mapping di EF sta nel <c>DbContext</c> e non nel dominio.
/// </summary>
/// <param name="From">Clausola base, es. <c>FROM "Brands" AS "brand"</c> (con l'alias usato ovunque nella query).</param>
/// <param name="BasePredicate">
/// Predicato sempre applicato in AND al filtro utente (es. soft-delete <c>NOT ("brand"."IsDeleted")</c>). Null = nessuno.
/// </param>
public sealed record SqlEntitySchema(string From, string? BasePredicate = null)
{
    /// <summary>
    /// Per i campi array/collezione: come diventano EXISTS/aggregazioni correlati (vedi <see cref="SqlArrayMapping"/>),
    /// per nome-campo pubblico. Vuoto se l'entità non ha collezioni ricercabili.
    /// </summary>
    public IReadOnlyDictionary<string, SqlArrayMapping> ArrayMappings { get; init; }
        = new Dictionary<string, SqlArrayMapping>();
}

/// <summary>
/// Sorgente delle configurazioni SQL per-entità (lookup per nome). L'implementazione di catalogo le hardcoda
/// in codice (vedi <c>CatalogSqlSchemaProvider</c>); resta un'interfaccia per poterla sostituire nei test.
/// </summary>
public interface ISqlSchemaProvider
{
    /// <summary>Config SQL per l'entità indicata; lancia se non registrata.</summary>
    SqlEntitySchema GetSchema(string entityName);
}
