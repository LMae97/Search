using Search.Core;
using Search.Core.Filters;
using Search.Linq;
using Search.Core.Metadata;

namespace Search.Tests;

/// <summary>
/// Il builder type-safe <c>SearchMap.For&lt;T&gt;()</c>: nome e tipo dei campi dedotti dalle espressioni, poi
/// esecuzione end-to-end su LINQ-to-Objects (nessuna infrastruttura). È la prova che la porta "code-first"
/// alimenta lo stesso motore.
/// </summary>
public sealed class TypedSearchMapBuilderTests
{
    private static readonly TestProduct[] Data =
    [
        new() { Id = Guid.NewGuid(), Name = "Alpha", Description = "primo", Price = 10m, Stock = 5,
                Tags = [new TestTag { Name = "sale" }] },
        new() { Id = Guid.NewGuid(), Name = "Beta", Description = "secondo", Price = 20m, Stock = 0,
                Tags = [new TestTag { Name = "sale" }, new TestTag { Name = "new" }] },
        new() { Id = Guid.NewGuid(), Name = "Gamma", Description = "alfa incluso", Price = 30m, Stock = 7,
                Tags = [] },
    ];

    private static IEntitySearchMap ProductMap() => LinqSearchMap.For<TestProduct>()
        .Field(p => p.Id)
        .Field(p => p.Name).Searchable()
        .Field(p => p.Description).Searchable()
        .Field(p => p.Price)
        .Field(p => p.Stock)
        .Field(p => p.Tags.Select(t => t.Name), "tags")
        .Build();

    [Fact]
    public void Kinds_are_inferred_from_the_property_types()
    {
        var map = ProductMap();

        Assert.Equal(FieldKind.Guid, Field(map, "id").Kind);
        Assert.Equal(FieldKind.String, Field(map, "name").Kind);
        Assert.Equal(FieldKind.Decimal, Field(map, "price").Kind);  // dedotto da decimal, non dichiarato
        Assert.Equal(FieldKind.Integer, Field(map, "stock").Kind);
    }

    [Fact]
    public void Field_name_is_derived_from_the_member_in_camel_case()
    {
        var map = ProductMap();

        Assert.True(map.TryGetField("name", out _));       // p => p.Name  →  "name"
        Assert.True(map.TryGetField("description", out _)); // p => p.Description → "description"
    }

    [Fact]
    public void Collection_projection_becomes_an_array_field()
    {
        var tags = Field(ProductMap(), "tags");

        Assert.True(tags.IsArray);
        Assert.Equal(FieldKind.String, tags.Kind); // elemento della collezione
    }

    [Fact]
    public void Searchable_flag_is_set_only_where_declared()
    {
        var map = ProductMap();

        Assert.True(Field(map, "name").IsSearchable);
        Assert.True(Field(map, "description").IsSearchable);
        Assert.False(Field(map, "price").IsSearchable);
    }

    // ---------------------------------------------------------------- end-to-end via l'extension LINQ

    [Fact]
    public void Search_extension_filters_and_projects()
    {
        var result = Data.Search(
            new SearchRequest { Filter = Filter.Gte("price", 20), Projection = ["name", "price"] },
            ProductMap());

        Assert.Equal(2, result.TotalCount); // Beta, Gamma
        Assert.All(result.Items, row => Assert.True(row.ContainsKey("name") && row.ContainsKey("price")));
    }

    /*
    [Fact]
    public void Free_text_or_contains_matches_any_searchable_field()
    {
        // "al" compare in Name="Alpha" (via name) e in Description="alfa incluso" (via description) → OR sui due campi.
        var result = Data.Search(new SearchRequest { Search = "al", Projection = ["name"] }, ProductMap());

        var names = result.Items.Select(r => (string)r["name"]!).OrderBy(n => n).ToList();
        Assert.Equal(["Alpha", "Gamma"], names);
    }
    */

    [Fact]
    public void Array_field_supports_contains_any()
    {
        var result = Data.Search(
            new SearchRequest { Filter = Filter.ArrayContainsAny("tags", "new"), Projection = ["name"] },
            ProductMap());

        var only = Assert.Single(result.Items);
        Assert.Equal("Beta", only["name"]);
    }

    private static FieldDescriptor Field(IEntitySearchMap map, string name)
    {
        Assert.True(map.TryGetField(name, out var field));
        return field!;
    }
}
