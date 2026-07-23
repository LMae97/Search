using MongoDB.Bson;
using Search.Application.Querying;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Metadata;
using Search.Infrastructure.Mongo;
using static Search.Tests.TestSupport;

namespace Search.Tests;

/// <summary>Costruzione del piano Mongo (proiezione/sort) senza toccare il database.</summary>
public sealed class MongoSearchExecutorTests
{
    private const string E = "compensationPlan";

    private static MongoSearchExecutor<BsonDocument> Executor() => new(Map(
        SearchEntity.Document(E), 
        Caller(),
        Def(E, "id", FieldKind.ObjectId, "_id"),
        Def(E, "name", FieldKind.String, "name"),
        Def(E, "createdAt", FieldKind.DateTime, "createdAt")));

    [Fact]
    public void Projection_includes_requested_paths()
    {
        var plan = Executor().BuildPlan(new SearchRequest { Projection = ["id", "name"] });

        Assert.Equal(1, plan.Projection["_id"].AsInt32);
        Assert.Equal(1, plan.Projection["name"].AsInt32);
    }

    [Fact]
    public void Id_is_excluded_when_not_projected() //TODO: QUESTO IN UN CASO REALE DI ACQUARIO SARA' DA TOGLIERE
    {
        var plan = Executor().BuildPlan(new SearchRequest { Projection = ["name"] });

        // Mongo includerebbe _id di default: il piano lo esclude esplicitamente se non richiesto.
        Assert.Equal(0, plan.Projection["_id"].AsInt32);
    }

    [Fact]
    public void No_sort_falls_back_to_default_id()
    {
        var plan = Executor().BuildPlan(new SearchRequest { Projection = ["name"] });

        Assert.NotNull(plan.Sort);
        Assert.Equal(1, plan.Sort!["_id"].AsInt32); // default sort su "id" → path "_id"
    }

    [Fact]
    public void Explicit_sort_maps_field_to_storage_path_and_direction()
    {
        var plan = Executor().BuildPlan(new SearchRequest
        {
            Projection = ["name"],
            Sort = [new SortField("name", SortDirection.Descending)]
        });

        Assert.Equal(-1, plan.Sort!["name"].AsInt32);
    }

    [Fact]
    public void Skip_and_limit_reflect_the_page()
    {
        var plan = Executor().BuildPlan(new SearchRequest { Projection = ["id"], Page = new PageRequest(3, 10) });

        Assert.Equal(20, plan.Skip); // (3-1)*10
        Assert.Equal(10, plan.Limit);
    }
}
