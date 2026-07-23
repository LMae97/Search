using Search.Application.Querying;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Filters;
using Search.Application.Querying.Metadata;
using Search.Application.Querying.Validation;
using static Search.Tests.TestSupport;

namespace Search.Tests;

/// <summary>La barriera di sicurezza: nessun campo/operatore fuori whitelist arriva al translator.</summary>
public sealed class SearchRequestValidatorTests
{
    private const string E = "customer";

    private static SearchRequestValidator Validator() => new(Map(
        SearchEntity.RelationalRaw(E), 
        Caller(),
        Def(E, "id", FieldKind.Guid, "\"c\".\"Id\""),
        Def(E, "name", FieldKind.String, "\"c\".\"Name\""),
        Def(E, "price", FieldKind.Decimal, "\"c\".\"Price\""),
        Def(E, "tags", FieldKind.String, "\"tag\".\"Name\"", isArray: true)));

    //Verifica che venga lanciata un'eccezione di validazione per la richiesta fornita.
    private static SearchValidationException Invalid(SearchRequest request)
        => Assert.Throws<SearchValidationException>(() => Validator().Validate(request));

    [Fact]
    public void Valid_request_passes()
    {
        var ex = Record.Exception(() => 
            Validator().Validate(new SearchRequest
            {
                Filter = Filter.Eq("name", "x"),
                Projection = ["id", "name"],
                Sort = [new SortField("name", SortDirection.Ascending)]
            }));

        Assert.Null(ex);
    }

    [Fact]
    public void Unknown_field_in_filter_is_rejected()
    {
        var ex = Invalid(new SearchRequest { Filter = Filter.Eq("ghost", "x") });
        Assert.Contains(ex.Errors, e => e.Contains("ghost"));
    }

    [Fact]
    public void Unknown_projection_and_sort_fields_are_rejected()
    {
        var ex = Invalid(new SearchRequest
        {
            Projection = ["ghost"],
            Sort = [new SortField("phantom", SortDirection.Ascending)]
        });

        Assert.Contains(ex.Errors, e => e.Contains("ghost"));
        Assert.Contains(ex.Errors, e => e.Contains("phantom"));
    }

    [Fact]
    public void Sort_on_array_field_is_rejected()
    {
        var ex = Invalid(new SearchRequest { Sort = [new SortField("tags", SortDirection.Ascending)] });
        Assert.Contains(ex.Errors, e => e.Contains("tags"));
    }

    [Fact]
    public void Between_requires_exactly_two_values()
    {
        var ex = Invalid(new SearchRequest { Filter = new ComparisonFilterNode("price", FilterOperator.Between, 10) });
        Assert.NotEmpty(ex.Errors);
    }

    [Fact]
    public void Operator_not_allowed_for_kind_is_rejected()
    {
        // GreaterThan non è ammesso su un campo String.
        var ex = Invalid(new SearchRequest { Filter = Filter.Gt("name", "x") });
        Assert.Contains(ex.Errors, e => e.Contains("name"));
    }

    [Theory]
    [InlineData(0, 20)]     // pagina < 1
    [InlineData(1, 0)]      // size < 1
    [InlineData(1, 500)]    // size > 200
    public void Invalid_pagination_is_rejected(int number, int size)
    {
        var ex = Invalid(new SearchRequest { Page = new PageRequest(number, size) });
        Assert.NotEmpty(ex.Errors);
    }
}
