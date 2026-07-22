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
        "customer" => Customer,
        "workprofile" => Workprofile,
        "utente" => User,
        _ => throw new InvalidOperationException($"Nessuna configurazione SQL per l'entità '{entityName}'.")
    };

    private static readonly SqlEntitySchema Customer = new(
        from: "FROM \"Customers\" AS \"customer\"",
        joins: new Dictionary<string, SqlJoin>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdByName"] = new SqlSimpleJoin("LEFT JOIN \"Users\" AS \"utenteCreatore\" ON \"utenteCreatore\".\"Id\" = \"customer\".\"CreatedById\""),
            ["updatedByName"] = new SqlSimpleJoin("LEFT JOIN \"Users\" AS \"utenteModificatore\" ON \"utenteModificatore\".\"Id\" = \"customer\".\"UpdatedById\""),
            ["tagIds"] = new SqlM2MJoin(CustomerTagJoin()),
            ["tagNames"] = new SqlM2MJoin(CustomerTagJoin("t.\"Name\""))
        });

    private static string CustomerTagJoin(params string[] args)
    {
        var select = "\"t\".\"Id\" AS \"Id\"";
        
        foreach (var arg in args) select += ", " + arg;

        return $"""
            FROM "CustomerTag" AS "ct"
            INNER JOIN "Tags" AS "t"
                ON "ct"."TagId" = "tag"."Id"
                AND "tag"."SpaceId" = @space
            WHERE customer."Id" = "ct"."CustomerId"
        """;
    }

    private static readonly SqlEntitySchema Workprofile = new(
        from: "FROM \"WorkProfiles\" AS \"workprofile\"",
        joins: new Dictionary<string, SqlJoin>(StringComparer.OrdinalIgnoreCase)
        {
            ["brandName"] = new SqlSimpleJoin("LEFT JOIN \"Brands\" AS \"brand\" ON \"brand\".\"Id\" = \"workprofile\".\"BrandId\""),
            ["userIds"] = new SqlM2MJoin(WorkprofileUserJoin()),
            ["userNames"] = new SqlM2MJoin(WorkprofileUserJoin("u.\"Username\""))
        });

    private static string WorkprofileUserJoin(params string[] args)
    {
        var select = "\"u\".\"Id\" AS \"Id\"";

        foreach ( var arg in args ) select += ", " + arg;

        return $"""
            FROM "UserWorkProfile" AS "uwp"
            INNER JOIN Users" AS "utente" 
                ON "uwp"."UserId" = "utente"."Id"
                AND "utente"."SpaceId" = @space
            WHERE workprofile."Id" = "uwp"."WorkProfileId"
        """;
    } 

    private static readonly SqlEntitySchema User = new(
        from: "FROM \"Users\" AS \"utente\"",
        basePredicate: "utente.\"SpaceId\" = @space AND utente.\"SoftDeleted\" = FALSE",
        joins: new Dictionary<string, SqlJoin>(StringComparer.OrdinalIgnoreCase)
        {
            ["workProfile"] = new SqlM2MJoin(UserWorkProfileJoin()),
            ["accountEmail"] = new SqlSimpleJoin("LEFT JOIN \"Accounts\" AS \"account\" ON \"account\".\"Id\" = \"utente\".\"AccountId\"")
        });

    private static string UserWorkProfileJoin()
    {
        return $"""
            FROM "UserWorkProfileReadOnly" AS "uwp"
            INNER JOIN "WorkProfiles" AS "workprofile"
                ON "uwp"."WorkProfileId" = "workprofile"."Id"
                AND "workprofile"."SpaceId" = @space
            INNER JOIN "Brands" AS "brand"
                ON "workprofile"."BrandId" = "brand"."Id"
                AND "brand"."SpaceId" = @space
            WHERE utente."Id" = "uwp"."UserId"
        """;
    }
}
