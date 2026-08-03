using WeByte.Search.Sql;

namespace WeByte.Search.Infrastructure.Sql;

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
        "user" => User,
        _ => throw new InvalidOperationException($"Nessuna configurazione SQL per l'entità '{entityName}'.")
    };

    private static readonly SqlEntitySchema Customer = new(
        from: "FROM \"Customers\" AS \"customer\"",
        basePredicate: "customer.\"SpaceId\" = @space",
        joins: new Dictionary<string, SqlJoin>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdByName"] = new SqlSimpleJoin("LEFT JOIN \"Users\" AS \"utenteCreatore\" ON \"utenteCreatore\".\"Id\" = \"customer\".\"CreatedById\""),
            ["updatedByName"] = new SqlSimpleJoin("LEFT JOIN \"Users\" AS \"utenteModificatore\" ON \"utenteModificatore\".\"Id\" = \"customer\".\"UpdatedById\""),
            ["contractOrgMemberIds"] = new SqlM2MJoin(CustomerContractOrgMemberJoin),
            ["contractAssignedToIds"] = new SqlM2MJoin(CustomerContractAssignedToJoin),
            ["contractOrgMemberOrganizationIds"] = new SqlM2MJoin(CustomerContractOrgMemberJoin),
            ["contractAssignedToOrganizationIds"] = new SqlM2MJoin(CustomerContractAssignedToJoin),
            ["tagIds"] = new SqlM2MJoin(CustomerTagJoin),
            ["tagNames"] = new SqlM2MJoin(CustomerTagJoin)
        });

    private const string CustomerContractOrgMemberJoin = $"""
            FROM "Contracts" AS "ctr"
            INNER JOIN "OrgMembers" AS "om"
                ON "ctr"."OrgMemberId" = "om"."Id"
                AND "om"."SpaceId" = @space
            WHERE customer."Id" = "ctr"."CustomerId"
        """;

    private const string CustomerContractAssignedToJoin = $"""
            FROM "Contracts" AS "ctr"
            LEFT JOIN "Users" AS "u"
                ON "ctr"."AssignedToId" = "u"."Id"
                AND "u"."SpaceId" = @space
            WHERE customer."Id" = "ctr"."CustomerId"
        """;

    private const string CustomerTagJoin = $"""
            FROM "CustomerTag" AS "ct"
            INNER JOIN "Tags" AS "tag"
                ON "ct"."TagId" = "tag"."Id"
                AND "tag"."SpaceId" = @space
            WHERE customer."Id" = "ct"."CustomerId"
        """;

    private static readonly SqlEntitySchema Workprofile = new(
        from: "FROM \"WorkProfiles\" AS \"workprofile\"",
        basePredicate: "workprofile.\"SpaceId\" = @space",
        joins: new Dictionary<string, SqlJoin>(StringComparer.OrdinalIgnoreCase)
        {
            // Il join va registrato per OGNI campo che lo richiede: la clausola FROM si compone dai soli
            // join dei campi effettivamente usati, quindi un campo non elencato qui manda in errore la
            // query ("missing FROM-clause entry") appena viene proiettato o filtrato.
            ["brandName"] = new SqlSimpleJoin(WorkprofileBrandJoin),
            ["contactEmail"] = new SqlSimpleJoin(WorkprofileBrandJoin),
            ["createdBy"] = new SqlSimpleJoin(
                "LEFT JOIN \"Users\" AS \"utentecreatore\" ON \"utentecreatore\".\"Id\" = \"workprofile\".\"CreatedById\""),
            ["updatedBy"] = new SqlSimpleJoin(
                "LEFT JOIN \"Users\" AS \"utentemodificatore\" ON \"utentemodificatore\".\"Id\" = \"workprofile\".\"UpdatedById\""),
            ["userIds"] = new SqlM2MJoin(WorkprofileUserJoin),
            ["userNames"] = new SqlM2MJoin(WorkprofileUserJoin)
        });

    private const string WorkprofileBrandJoin =
        "LEFT JOIN \"Brands\" AS \"brand\" ON \"brand\".\"Id\" = \"workprofile\".\"BrandId\"";

    private const string WorkprofileUserJoin = $"""
            FROM "UserWorkProfile" AS "uwp"
            INNER JOIN "Users" AS "utente"
                ON "uwp"."UserId" = "utente"."Id"
                AND "utente"."SpaceId" = @space
            WHERE workprofile."Id" = "uwp"."WorkProfileId"
        """;

    private static readonly SqlEntitySchema User = new(
        from: "FROM \"Users\" AS \"utente\"",
        basePredicate: "utente.\"SpaceId\" = @space AND utente.\"SoftDeleted\" = FALSE",
        joins: new Dictionary<string, SqlJoin>(StringComparer.OrdinalIgnoreCase)
        {
            ["workProfile"] = new SqlM2MJoin(UserWorkProfileJoin),
            ["workProfiles"] = new SqlM2MJoin(UserWorkProfileJoin),
            ["brand"] = new SqlM2MJoin(UserWorkProfileJoin),
            ["workProfileId"] = new SqlM2MJoin(UserWorkProfileJoin),
            ["brandId"] = new SqlM2MJoin(UserWorkProfileJoin),
            ["accountEmail"] = new SqlSimpleJoin(AccountJoin),
            ["sex"] = new SqlSimpleJoin(AccountJoin),
            ["birthDate"] = new SqlSimpleJoin(AccountJoin),
            ["organization"] = new SqlSimpleJoin("LEFT JOIN \"Organizations\" AS \"organization\" ON \"organization\".\"Id\" = \"utente\".\"OrganizationId\""),
            ["typology"] = new SqlSimpleJoin("LEFT JOIN \"Typologies\" AS \"typology\" ON \"typology\".\"Id\" = \"utente\".\"TypologyId\""),
            ["roleIds"] = new SqlM2MJoin(UserRoleJoin),
            ["roles"] = new SqlM2MJoin(UserRoleJoin),
            ["tagIds"] = new SqlM2MJoin(UserTagJoin),
            ["tags"] = new SqlM2MJoin(UserTagJoin),
            ["createdBy"] = new SqlSimpleJoin("LEFT JOIN \"Users\" AS \"utenteCreatore\" ON \"utenteCreatore\".\"Id\" = \"utente\".\"CreatedById\""),
            ["updatedBy"] = new SqlSimpleJoin("LEFT JOIN \"Users\" AS \"utenteModificatore\" ON \"utenteModificatore\".\"Id\" = \"utente\".\"UpdatedById\"")
        });

    private const string AccountJoin =
        "LEFT JOIN \"Accounts\" AS \"account\" ON \"account\".\"Id\" = \"utente\".\"AccountId\"";

    private const string UserWorkProfileJoin = $"""
            FROM "UserWorkProfileReadOnly" AS "uwp"
            INNER JOIN "WorkProfiles" AS "workprofile"
                ON "uwp"."WorkProfileId" = "workprofile"."Id"
                AND "workprofile"."SpaceId" = @space
            INNER JOIN "Brands" AS "brand"
                ON "workprofile"."BrandId" = "brand"."Id"
                AND "brand"."SpaceId" = @space
            WHERE utente."Id" = "uwp"."UserId"
        """;

    private const string UserRoleJoin = $"""
            FROM "UserRole" AS "ur"
            INNER JOIN "Roles" AS "role"
                ON "ur"."RoleId" = "role"."Id"
            WHERE utente."Id" = "ur"."UserId"
        """;

    private const string UserTagJoin = $"""
            FROM "UserTag" AS "ut"
            INNER JOIN "Tags" AS "tag"
                ON "ut"."TagId" = "tag"."Id"
                AND "tag"."SpaceId" = @space
            WHERE utente."Id" = "ut"."UserId"
        """;
}