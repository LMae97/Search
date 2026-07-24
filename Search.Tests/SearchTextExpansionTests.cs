using Search.Application.Config;
using Search.Application.Querying;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Filters;
using Search.Application.Querying.Metadata;
using System.ComponentModel;
using static Search.Tests.TestSupport;

namespace Search.Tests;

/// <summary>
/// Espansione del free-text secondo la strategia d'entità: OrContains si traduce in un sotto-albero di filtri
/// riusabile da ogni store; Atlas resta intatto per lo store Mongo.
/// </summary>
public sealed class SearchTextExpansionTests
{
    private const string E = "e";

    private static IEntitySearchMap MapWithSearchable() => Map(
        SearchEntity.Document(E), Caller(),
        Def(E, "name", FieldKind.String, "name", searchable: true),
        Def(E, "description", FieldKind.String, "description", searchable: true),
        Def(E, "code", FieldKind.String, "code")); // NON searchable

    private static SearchRequest OrContains(SearchRequest req) =>
        SearchTextExpansion.Apply(FreeTextSearch.OrContains, MapWithSearchable(), req);

    [Fact]
    public void OrContains_expands_free_text_into_an_or_of_contains_over_searchable_fields()
    {
        var result = OrContains(new SearchRequest { Search = "mario" });

        Assert.Null(result.Search); // consumato
        var or = Assert.IsType<LogicalFilterNode>(result.Filter);
        Assert.Equal(LogicalOperator.Or, or.Operator);
        Assert.Equal(2, or.Children.Count); // name, description — NON code

        foreach (var child in or.Children.Cast<ComparisonFilterNode>())
        {
            Assert.Equal(FilterOperator.Contains, child.Operator);
            Assert.Equal("mario", child.SingleValue);
        }

        var fields = or.Children.Cast<ComparisonFilterNode>().Select(c => c.Field).ToList();
        Assert.Contains("name", fields);
        Assert.Contains("description", fields);
        Assert.DoesNotContain("code", fields);
    }

    [Fact]
    public void OrContains_ands_the_free_text_with_an_existing_filter()
    {
        var result = OrContains(new SearchRequest { Search = "mario", Filter = Filter.Eq("code", "X") });

        var and = Assert.IsType<LogicalFilterNode>(result.Filter);
        Assert.Equal(LogicalOperator.And, and.Operator);
        Assert.Equal(2, and.Children.Count);
        // un ramo è il filtro originale, l'altro è l'OR di contains
        Assert.Contains(and.Children, c => c is LogicalFilterNode { Operator: LogicalOperator.Or });
        Assert.Contains(and.Children, c => c is ComparisonFilterNode { Field: "code" });
    }

    [Fact]
    public void Atlas_leaves_the_request_untouched()
    {
        var result = SearchTextExpansion.Apply(
            FreeTextSearch.Atlas("compensation_search"), MapWithSearchable(),
            new SearchRequest { Search = "mario" });

        Assert.Equal("mario", result.Search); // intatto: lo tradurrà lo store Mongo ($search)
        Assert.Null(result.Filter);
    }

    [Fact]
    public void Empty_free_text_is_a_no_op()
    {
        var result = OrContains(new SearchRequest { Search = "   " });

        Assert.Null(result.Filter);
        Assert.Equal("   ", result.Search);
    }

    [Fact]
    public void No_searchable_fields_means_no_filter_is_added()
    {
        var mapWithoutSearchable = Map(
            SearchEntity.Document(E), Caller(),
            Def(E, "code", FieldKind.String, "code"));

        var result = SearchTextExpansion.Apply(
            FreeTextSearch.OrContains, mapWithoutSearchable, new SearchRequest { Search = "mario" });

        Assert.Null(result.Filter); // niente dove cercare → nessun filtro
    }
}