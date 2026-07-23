using Search.Application.Querying;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Metadata;
using Search.Infrastructure.Sql;
using static Search.Tests.TestSupport;

namespace Search.Tests;

/// <summary>Assemblaggio della query SQL completa: proiezione, join condizionali, @space, ORDER BY di default, count.</summary>
public sealed class SqlSearchQueryBuilderTests
{
    private const string E = "customer";

    private static IEntitySearchMap BuildMap() => Map(
        SearchEntity.RelationalRaw(E), 
        Caller(),
        Def(E, "id", FieldKind.Guid, "\"c\".\"Id\""),
        Def(E, "name", FieldKind.String, "\"c\".\"Name\""),
        Def(E, "createdByName", FieldKind.String, "\"u\".\"Username\""));

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
}
