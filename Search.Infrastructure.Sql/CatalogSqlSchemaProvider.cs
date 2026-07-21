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
        "product" => Product,
        "customer" => Customer,
        _ => throw new InvalidOperationException($"Nessuna configurazione SQL per l'entità '{entityName}'.")
    };


    // brand → tabella "Brands" (alias "brand"), soft-delete su IsDeleted, tag via tabella ponte "BrandTag".
    private static readonly SqlEntitySchema Brand = new(
        from: "FROM \"Brands\" AS \"brand\"",
        basePredicate: "NOT (\"brand\".\"IsDeleted\")", // soft-delete: sempre in AND col filtro utente
        joins: new Dictionary<string, SqlJoin>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = new SqlArrayMapping(BrandTagJoin, "\"tag\".\"Name\"", "SELECT COALESCE(array_agg(\"tag\".\"Name\"), ARRAY[]::text[]) " + BrandTagJoin),
            ["tagIds"] = new SqlArrayMapping(BrandTagJoin, "\"tag\".\"Id\"", "SELECT COALESCE(array_agg(\"tag\".\"Id\"), ARRAY[]::uuid[]) " + BrandTagJoin),
            ["dataTags"] = new SqlArrayMapping( //TODO: QUESTO POTREBBE DIVENTARE UN JSON MAPPING
                From: "FROM jsonb_array_elements_text(coalesce(\"brand\".\"Data\" -> 'tags','[]'::jsonb)) AS elem WHERE true",
                ElementColumn: "elem",
                Projection: "\"brand\".\"Data\" -> 'tags'")
        });

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

    private static readonly SqlEntitySchema Product = new(
        from: "FROM \"Products\" AS \"product\"",
        basePredicate: "NOT (\"product\".\"IsDeleted\")", // soft-delete: sempre in AND col filtro utente
        joins: new Dictionary<string, SqlJoin>(StringComparer.OrdinalIgnoreCase)
        {
            ["brandName"] = new SqlSimpleJoin("LEFT JOIN \"Brands\" AS \"brand\" ON \"brand\".\"Id\" = \"product\".\"BrandId\""),
            ["tags"] = new SqlArrayMapping(
                From: ProductTagJoin, 
                ElementColumn: "\"tag\".\"Name\"",
                Projection: $"(SELECT COALESCE(array_agg(\"tag\".\"Name\"), ARRAY[]::text[]) {ProductTagJoin})"),
            ["tagIds"] = new SqlArrayMapping(
                From: ProductTagJoin,
                ElementColumn: "\"tag\".\"Id\"",
                Projection: $"(SELECT COALESCE(array_agg(\"tag\".\"Id\"), ARRAY[]::uuid[]) {ProductTagJoin})"),
        });

    private const string ProductTagJoin = """
        FROM "ProductTag" AS "pt"
        INNER JOIN (
            SELECT "t"."Id", "t"."Name"
            FROM "Tags" AS "t"
            WHERE NOT ("t"."IsDeleted")
        ) AS "tag" ON "pt"."TagsId" = "tag"."Id"
        WHERE "product"."Id" = "pt"."ProductId"
        """;

    private static readonly SqlEntitySchema Customer = new(
        from: "FROM \"Customers\" AS \"customer\"",
        joins: new Dictionary<string, SqlJoin>(StringComparer.OrdinalIgnoreCase)
        {
            ["landline"] = new SqlArrayMapping(
                From: "FROM jsonb_array_elements_text(coalesce(\"customer\".\"Landline\", '[]'::jsonb)) AS elem WHERE true",
                ElementColumn: "elem",
                Projection: "\"customer\".\"Landline\"")
        });
}
