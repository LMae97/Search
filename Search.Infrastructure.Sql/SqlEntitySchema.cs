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
/// Per i campi array/collezione: come diventano EXISTS/aggregazioni correlati (vedi <see cref="SqlM2MJoin"/>),
/// per nome-campo pubblico. Vuoto se l'entità non ha collezioni ricercabili.
/// </param>
public sealed class SqlEntitySchema
{
    public string From { get; private set; }
    public string? BasePredicate { get; private set; }
    private readonly IReadOnlyDictionary<string, SqlJoin> Joins;

    public SqlEntitySchema(
        string from,
        string? basePredicate = null,
        IReadOnlyDictionary<string, SqlJoin>? joins = null)
    {
        From = from;
        BasePredicate = basePredicate;
        Joins = joins ?? new Dictionary<string, SqlJoin>();
    }

    public IReadOnlyDictionary<string, SqlM2MJoin> GetM2MJoins()
        => Joins
            .Where(kv => kv.Value is SqlM2MJoin)
            .ToDictionary(kv => kv.Key, kv => (SqlM2MJoin)kv.Value);

    public IReadOnlyDictionary<string, SqlSimpleJoin> GetSimpleJoins()
        => Joins
            .Where(kv => kv.Value is SqlSimpleJoin)
            .ToDictionary(kv => kv.Key, kv => (SqlSimpleJoin)kv.Value);
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
