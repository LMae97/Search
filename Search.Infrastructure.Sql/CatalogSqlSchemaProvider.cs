namespace Search.Infrastructure.Sql;

/// <summary>
/// Configurazioni SQL delle entità del catalogo per lo store "raw" (<see cref="Dynamic.StoreKind.PostgresRaw"/>),
/// <b>hardcodate in codice</b> (non lette da DB): FROM base, soft-delete e join delle collezioni cambiano solo
/// con una migrazione/deploy → stanno qui, esattamente come il fluent mapping di EF sta nel <c>CatalogDbContext</c>.
/// (Le <i>definizioni dei campi</i> ricercabili restano invece data-driven — vedi <c>SimulatedFieldDefinitionDatabase</c>.)
/// </summary>
public sealed class CatalogSqlSchemaProvider : ISqlSchemaProvider
{
    public SqlEntitySchema GetSchema(string entityName) => entityName.ToLowerInvariant() switch
    {
        "brand" => Brand,
        _ => throw new InvalidOperationException($"Nessuna configurazione SQL per l'entità '{entityName}'.")
    };

    // brand → tabella "Brands" (alias "brand"), soft-delete su IsDeleted, tag via tabella ponte "BrandTag".
    private static readonly SqlEntitySchema Brand = new(
        From: "FROM \"Brands\" AS \"brand\"",
        BasePredicate: "NOT (\"brand\".\"IsDeleted\")") // soft-delete: sempre in AND col filtro utente
    {
        ArrayFilters = new Dictionary<string, SqlArrayFilter>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = new SqlArrayFilter(BrandTagJoin, "\"tag\".\"Name\""),
            ["tagIds"] = new SqlArrayFilter(BrandTagJoin, "\"tag\".\"Id\""),
        }
    };

    // Correlazione col padre inclusa: è tutto ciò che segue "SELECT 1" nell'EXISTS (filtro) e nell'aggregazione
    // json_group_array (proiezione). Un'unica definizione riusata da entrambi.
    private const string BrandTagJoin = """
        FROM "BrandTag" AS "bt"
        INNER JOIN (
            SELECT "t"."Id", "t"."Name"
            FROM "Tags" AS "t"
            WHERE NOT ("t"."IsDeleted")
        ) AS "tag" ON "bt"."TagsId" = "tag"."Id"
        WHERE "brand"."Id" = "bt"."BrandId"
        """;
}
