using MongoDB.Bson;
using Search.Core;
using Search.Core.Dynamic;
using Search.Core.Metadata;
using Search.Mongo;
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
        Def(E, "name", FieldKind.String, "name", searchable: true),
        Def(E, "description", FieldKind.String, "description", searchable: true),
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

    // ---------------------------------------------------------------- free-text (Atlas $search)

    [Fact]
    public void No_free_text_means_no_search_stage()
    {
        var plan = Executor().BuildPlan(new SearchRequest { Projection = ["name"] });

        Assert.Null(plan.SearchStage); // → l'executor userà la find classica
    }

    [Fact]
    public void Free_text_builds_a_compound_should_over_every_searchable_field()
    {
        var plan = Executor().BuildPlan(new SearchRequest { Search = "mario", Projection = ["name"] });

        Assert.NotNull(plan.SearchStage);
        var search = plan.SearchStage!["$search"].AsBsonDocument;
        var should = search["compound"]["should"].AsBsonArray;

        // un ramo autocomplete per ogni campo searchable (name, description), tutti sulla stessa query
        Assert.Equal(2, should.Count);
        Assert.Equal(1, search["compound"]["minimumShouldMatch"].AsInt32);
        Assert.Equal("mario", should[0]["autocomplete"]["query"].AsString);
        var paths = should.Select(b => b["autocomplete"]["path"].AsString).ToList();
        Assert.Contains("name", paths);
        Assert.Contains("description", paths);
    }

    [Fact]
    public void Free_text_without_explicit_sort_leaves_ordering_to_relevance()
    {
        var plan = Executor().BuildPlan(new SearchRequest { Search = "mario", Projection = ["name"] });

        Assert.Null(plan.Sort); // niente $sort → ordina lo score di Atlas
    }

    [Fact]
    public void Multi_token_query_asks_for_sequential_token_order()
    {
        var plan = Executor().BuildPlan(new SearchRequest { Search = "  mario   rossi  ", Projection = ["name"] });

        var should = plan.SearchStage!["$search"]["compound"]["should"].AsBsonArray;
        Assert.Equal("mario rossi", should[0]["autocomplete"]["query"].AsString); // spazi collassati
        Assert.Equal("sequential", should[0]["autocomplete"]["tokenOrder"].AsString);
    }

    // ---------------------------------------------------------------- unwind (es. prodotti di un contratto)

    private static MongoSearchExecutor<BsonDocument> UnwindingExecutor() => new(
        Map(SearchEntity.Document(E), Caller(),
            Def(E, "id", FieldKind.ObjectId, "_id"),
            Def(E, "name", FieldKind.String, "name")),
        unwindPath: "contractData._products");

    [Fact]
    public void No_unwind_path_means_no_unwind_stage()
    {
        var plan = Executor().BuildPlan(new SearchRequest { Projection = ["name"] });

        Assert.Null(plan.UnwindStage);
    }

    [Fact]
    public void Unwind_path_builds_the_matching_stage()
    {
        var plan = UnwindingExecutor().BuildPlan(new SearchRequest { Projection = ["name"] });

        Assert.NotNull(plan.UnwindStage);
        Assert.Equal("$contractData._products", plan.UnwindStage!["$unwind"].AsString);
    }

    [Fact]
    public void Unwind_alone_without_free_text_still_requires_the_aggregate_path()
    {
        // Nessun $search, ma l'unwind da solo basta a impedire la find() classica (una find non può esprimere $unwind).
        var plan = UnwindingExecutor().BuildPlan(new SearchRequest { Projection = ["name"] });

        Assert.Null(plan.SearchStage);
        Assert.NotNull(plan.UnwindStage);
    }
}
