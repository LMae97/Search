using Search.Application.Querying;
using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Filters;
using Search.Application.Querying.Metadata;
using static Search.Tests.TestSupport;

namespace Search.Tests;

/// <summary>Il sanitizer POTA (non rifiuta) i riferimenti a campi assenti, prima della validazione dura.</summary>
public sealed class SearchRequestSanitizerTests
{
    private const string E = "customer";

    private static SearchRequestSanitizer Sanitizer() => new(Map(
        SearchEntity.RelationalRaw(E), 
        Caller(),
        Def(E, "id", FieldKind.Guid, "\"c\".\"Id\""),
        Def(E, "name", FieldKind.String, "\"c\".\"Name\"")));

    [Fact]
    public void Unknown_projection_and_sort_fields_are_pruned()
    {
        var result = Sanitizer().Sanitize(new SearchRequest
        {
            Projection = ["name", "ghost"],
            Sort = [new SortField("name", SortDirection.Ascending), new SortField("ghost", SortDirection.Ascending)]
        });

        Assert.Equal(new[] { "name" }, result.Projection);
        Assert.Equal(new[] { "name" }, result.Sort.Select(s => s.Field));
    }

    [Fact]
    public void Unknown_filter_leaf_is_pruned_and_and_collapses_to_the_survivor()
    {
        var result = Sanitizer().Sanitize(new SearchRequest
        {
            Filter = Filter.And(Filter.Eq("name", "x"), Filter.Eq("ghost", "y")),
            Projection = ["name"]
        });

        // AND con un solo figlio superstite ⇒ il figlio stesso.
        var leaf = Assert.IsType<ComparisonFilterNode>(result.Filter);
        Assert.Equal("name", leaf.Field);
    }

    [Fact]
    public void Fully_unknown_filter_is_removed()
    {
        var result = Sanitizer().Sanitize(new SearchRequest { Filter = Filter.Eq("ghost", "y") });

        Assert.Null(result.Filter);
    }

    [Fact]
    public void Empty_projection_stays_empty()
    {
        // Il sanitizer non applica più fallback di proiezione: potatura pura.
        var result = Sanitizer().Sanitize(new SearchRequest { Projection = [] });

        Assert.Empty(result.Projection);
    }
}
