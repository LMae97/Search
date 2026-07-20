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
/// <param name="ArrayMappings">
/// Per i campi array/collezione: come diventano EXISTS/aggregazioni correlati (vedi <see cref="SqlArrayMapping"/>),
/// per nome-campo pubblico. Vuoto se l'entità non ha collezioni ricercabili.
/// </param>
public sealed class SqlEntitySchema
{
    public string From { get; private set; }
    public string? BasePredicate { get; private set; }
    public IReadOnlyDictionary<string, SqlArrayMapping> CollectionJoins { get; private set; }

    /// <summary>
    /// Join OPZIONALI per-campo scalare: nome-campo → clausola JOIN. Il builder li aggiunge al FROM
    /// <b>solo se</b> il campo è usato (proiezione/filtro/sort) e li deduplica (più campi possono condividere
    /// lo stesso join). Es. <c>"brandName" → LEFT JOIN "Brands" AS "brand" …</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> ScalarJoins { get; private set; }

    public SqlEntitySchema(
        string from,
        string? basePredicate = null,
        IReadOnlyDictionary<string, SqlArrayMapping>? collectionJoins = null,
        IReadOnlyDictionary<string, string>? scalarJoins = null)
    {
        From = from;
        BasePredicate = basePredicate;
        CollectionJoins = collectionJoins ?? new Dictionary<string, SqlArrayMapping>();
        ScalarJoins = scalarJoins ?? new Dictionary<string, string>();
    }
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
