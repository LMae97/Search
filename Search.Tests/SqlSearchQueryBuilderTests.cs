using WeByte.Search.Core;
using WeByte.Search.Core.Dynamic;
using WeByte.Search.Core.Metadata;
using WeByte.Search.Sql;
using static WeByte.Search.Tests.TestSupport;

namespace WeByte.Search.Tests;

/// <summary>Assemblaggio della query SQL completa: proiezione, join condizionali, @space, ORDER BY di default, count.</summary>
public sealed class SqlSearchQueryBuilderTests
{
    private const string E = "customer";

    private static IEntitySearchMap BuildMap() => Map(
        SearchEntity.RelationalRaw(E),
        Caller(),
        Def(E, "id", FieldKind.Guid, "\"c\".\"Id\""),
        Def(E, "name", FieldKind.String, "\"c\".\"Name\""),
        Def(E, "createdByName", FieldKind.String, "\"u\".\"Username\""),
        // Un pulsante "custom" che porta un dato di corredo insieme al valore principale (es. il vecchio
        // "scarica tutti gli allegati", che tornava anche se il contratto fosse già stampato).
        Def(E, "btnDownloadAll", FieldKind.Custom, "\"c\".\"Id\"",
            secondaryPath: "\"c\".\"IsPrinted\"", secondaryKey: "isPrinted"));

    // Schema con un join scalare (createdByName) e un base-predicate che referenzia @space (scoping tenant).
    private static SqlEntitySchema Schema() => new(
        from: "FROM \"Customers\" AS \"c\"",
        basePredicate: "\"c\".\"SpaceId\" = @space",
        joins: new Dictionary<string, SqlJoin>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdByName"] = new SqlSimpleJoin("LEFT JOIN \"Users\" AS \"u\" ON \"u\".\"Id\" = \"c\".\"CreatedById\"")
        });

    //SUT: System Under Test
    private static SqlSearchQueryBuilder Builder() => new(BuildMap(), Schema());

    private static readonly Guid SpaceId = Guid.Parse("aaaa1111-2222-3333-4444-555566667777");

    [Fact]
    public void Projection_emits_aliased_columns()
    {
        var plan = Builder().Build(new SearchRequest { Projection = ["id", "name"] }, SpaceId);

        Assert.Contains("\"c\".\"Id\" AS \"id\"", plan.Sql);
        Assert.Contains("\"c\".\"Name\" AS \"name\"", plan.Sql);
    }

    [Fact]
    public void Scalar_join_is_emitted_only_when_its_field_is_used()
    {
        var withJoin = Builder().Build(new SearchRequest { Projection = ["id", "createdByName"] }, SpaceId);
        var withoutJoin = Builder().Build(new SearchRequest { Projection = ["id", "name"] }, SpaceId);

        Assert.Contains("LEFT JOIN \"Users\"", withJoin.Sql);
        Assert.DoesNotContain("LEFT JOIN \"Users\"", withoutJoin.Sql);
    }

    [Fact]
    public void Space_parameter_is_bound_because_base_predicate_references_it()
    {
        var plan = Builder().Build(new SearchRequest { Projection = ["id"] }, SpaceId);

        Assert.Contains("\"c\".\"SpaceId\" = @space", plan.Sql);
        Assert.Equal(SpaceId, plan.Parameters["@space"]);
    }

    [Fact]
    public void No_sort_falls_back_to_default_id_order()
    {
        var plan = Builder().Build(new SearchRequest { Projection = ["name"] }, SpaceId);

        Assert.Contains("ORDER BY \"c\".\"Id\" ASC", plan.Sql);
    }

    [Fact]
    public void Explicit_sort_is_respected()
    {
        var plan = Builder().Build(new SearchRequest
        {
            Projection = ["name"],
            Sort = [new SortField("name", SortDirection.Descending)]
        }, SpaceId);

        Assert.Contains("ORDER BY \"c\".\"Name\" DESC", plan.Sql);
    }

    [Fact]
    public void Data_query_paginates_with_limit_offset()
    {
        var plan = Builder().Build(new SearchRequest 
        { 
            Projection = ["id"], 
            Page = new PageRequest(3, 10) 
        }, SpaceId);

        Assert.Contains("LIMIT @take OFFSET @skip", plan.Sql);
        Assert.Equal(10, plan.Parameters["@take"]);
        Assert.Equal(20, plan.Parameters["@skip"]); // (3-1)*10
    }

    [Fact]
    public void Count_query_has_no_projection_or_pagination()
    {
        var plan = Builder().BuildCount(new SearchRequest 
        { 
            Projection = ["id", "name"] 
        }, SpaceId);

        Assert.Contains("SELECT COUNT(*)", plan.Sql);
        Assert.DoesNotContain("LIMIT", plan.Sql);
        Assert.DoesNotContain("AS \"name\"", plan.Sql);
    }

    [Fact]
    public void Capped_count_wraps_a_limited_subquery()
    {
        var plan = Builder().BuildCount(new SearchRequest(), SpaceId, upTo: 260);

        // COUNT(*) su una subquery già LIMIT-ata: Postgres si ferma dopo 260 righe candidate, non scandisce
        // tutta la tabella per poi scartare l'eccedenza.
        Assert.Contains("SELECT COUNT(*) FROM (", plan.Sql);
        Assert.Contains("LIMIT @countLimit", plan.Sql);
        Assert.Equal(260L, plan.Parameters["@countLimit"]);
    }

    [Fact]
    public void Without_a_cap_the_count_has_no_limit_at_all()
    {
        var plan = Builder().BuildCount(new SearchRequest(), SpaceId);

        Assert.DoesNotContain("LIMIT", plan.Sql);
        Assert.False(plan.Parameters.ContainsKey("@countLimit"));
    }

    [Fact]
    public void A_field_with_a_secondary_path_projects_a_composite_object()
    {
        var plan = Builder().Build(new SearchRequest { Projection = ["btnDownloadAll"] }, SpaceId);

        // Stessa forma del composto {value,label} di Link, ma con la chiave arbitraria "isPrinted".
        Assert.Contains(
            "json_build_object('value', \"c\".\"Id\", 'isPrinted', \"c\".\"IsPrinted\") AS \"btnDownloadAll\"",
            plan.Sql);
    }
}
