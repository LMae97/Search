using Search.Application.Querying;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Filters;
using Search.Application.Querying.Linq;
using Search.Application.Querying.Metadata;
using static Search.Tests.TestSupport;

namespace Search.Tests;

/// <summary>
/// Esecuzione end-to-end su LINQ-to-Objects: filtro + sort + proiezione + paginazione girano davvero,
/// senza infrastruttura. È il test più fedele della semantica di ricerca del motore.
/// </summary>
public sealed class LinqSearchExecutorTests
{
    private const string E = "product";

    private static readonly TestProduct[] Data =
    [
        new() { 
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), 
            Name = "Alpha", 
            Price = 10m, 
            Stock = 5, 
            Tags = [
                new TestTag { Name = "sale" }
            ] 
        },
        new() { 
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), 
            Name = "Beta",  
            Price = 20m, 
            Stock = 0, 
            Tags = [
                new TestTag { Name = "sale" }, 
                new TestTag { Name = "new" }
            ] 
        },
        new() { 
            Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), 
            Name = "Gamma", 
            Price = 30m, 
            Stock = 7, 
            Tags = [] 
        },
    ];

    private static IEntitySearchMap BuildMap() => Map(
        SearchEntity.RelationalEF<TestProduct>(E), 
        Caller(),
        Def(E, "id", FieldKind.Guid, "Id", defaultOrder: 0),
        Def(E, "name", FieldKind.String, "Name", defaultOrder: 1),
        Def(E, "description", FieldKind.String, "Description"),
        Def(E, "price", FieldKind.Decimal, "Price"),
        Def(E, "stock", FieldKind.Integer, "Stock"),
        Def(E, "tags", FieldKind.String, "Tags.Name", isArray: true));

    private static SearchResult<IReadOnlyDictionary<string, object?>> Run(SearchRequest request) =>
        new LinqSearchExecutor<TestProduct>(BuildMap()).Execute(Data.AsQueryable(), request);

    [Fact]
    public void Gte_filters_and_projects_only_requested_fields()
    {
        var result = Run(new SearchRequest
        {
            Filter = Filter.Gte("price", 20),
            Projection = ["name", "price"]
        });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, row => Assert.Equal(new[] { "name", "price" }, row.Keys.OrderBy(k => k)));
        Assert.Equal(new[] { "Beta", "Gamma" }, result.Items.Select(r => r["name"]).OrderBy(n => (string)n!));
        Assert.Equal(new[] { 20m, 30m }, result.Items.Select(r => (decimal)r["price"]!).OrderBy(p => (decimal)p!));
    }

    [Fact]
    public void Contains_is_case_insensitive()
    {
        var result = Run(new SearchRequest 
        { 
            Filter = Filter.Contains("name", "ALP"), 
            Projection = ["name"] 
        });

        var only = Assert.Single(result.Items);
        Assert.Equal("Alpha", only["name"]);
    }

    [Fact]
    public void Between_is_inclusive_on_both_ends()
    {
        var result = Run(new SearchRequest 
        { 
            Filter = Filter.Between("price", 10, 20), 
            Projection = ["name"] 
        });

        Assert.Equal(2, result.TotalCount); // 10 e 20 inclusi, 30 escluso
    }

    [Fact]
    public void ArrayContainsAny_matches_when_at_least_one_tag_present()
    {
        var result = Run(new SearchRequest 
        { 
            Filter = Filter.ArrayContainsAny("tags", "new"), 
            Projection = ["name"] 
        });

        var only = Assert.Single(result.Items);
        Assert.Equal("Beta", only["name"]);
    }

    [Fact]
    public void ArrayIsEmpty_matches_only_rows_without_tags()
    {
        var result = Run(new SearchRequest 
        { 
            Filter = Filter.ArrayIsEmpty("tags"), 
            Projection = ["name"] 
        });

        var only = Assert.Single(result.Items);
        Assert.Equal("Gamma", only["name"]);
    }

    [Fact]
    public void Sort_descending_orders_results()
    {
        var result = Run(new SearchRequest
        {
            Sort = [new SortField("price", SortDirection.Descending)],
            Projection = ["name"]
        });

        Assert.Equal(new[] { "Gamma", "Beta", "Alpha" }, result.Items.Select(r => (string)r["name"]!));
    }

    [Fact]
    public void No_sort_falls_back_to_deterministic_default_id()
    {
        var result = Run(new SearchRequest { Projection = ["id"] });

        // Default sort su "id": ordine crescente per Id (deterministico per la paginazione).
        Assert.Equal(
            Data.OrderBy(p => p.Id).Select(p => p.Id).ToArray(),
            result.Items.Select(r => (Guid)r["id"]!).ToArray());
    }

    [Fact]
    public void Pagination_returns_page_slice_and_full_total()
    {
        var result = Run(new SearchRequest
        {
            Sort = [new SortField("price", SortDirection.Ascending)],
            Projection = ["name"],
            Page = new PageRequest(2, 1) // seconda pagina, 1 per pagina
        });

        Assert.Equal(3, result.TotalCount);      // il totale ignora la paginazione
        var only = Assert.Single(result.Items);
        Assert.Equal("Beta", only["name"]);      // 2° elemento per prezzo crescente
    }
}
