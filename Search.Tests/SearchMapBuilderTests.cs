using MongoDB.Bson;
using Search.Core;
using Search.Core.Fluent;
using Search.Core.Metadata;
using Search.Mongo;

namespace Search.Tests;

/// <summary>
/// Il builder code-first: costruisce una mappa in poche righe e produce gli stessi descrittori del percorso
/// data-driven (stesso resolver). Verifica anche che la mappa si agganci al motore Mongo senza DB.
/// </summary>
public sealed class SearchMapBuilderTests
{
    private static IEntitySearchMap ProductsMap() => SearchMap.ForDocument("products")
        .Field("id", FieldKind.ObjectId, "_id")
        .Field("name", FieldKind.String).Searchable().Label("Nome")
        .Field("description", FieldKind.String).Searchable()
        .Field("price", FieldKind.Decimal)
        .Field("secret", FieldKind.String).Hidden()
        .Build();

    [Fact]
    public void Builds_fields_with_kinds_paths_and_decorators()
    {
        var map = ProductsMap();

        Assert.True(map.TryGetField("id", out var id));
        Assert.Equal(FieldKind.ObjectId, id!.Kind);
        Assert.Equal("_id", id.StoragePath); // path esplicito

        Assert.True(map.TryGetField("name", out var name));
        Assert.Equal("name", name!.StoragePath); // path assente ⇒ = nome
        Assert.True(name.IsSearchable);
        Assert.Equal("Nome", name.Label);

        Assert.True(map.TryGetField("price", out var price));
        Assert.Equal(FieldKind.Decimal, price!.Kind);
        Assert.False(price.IsSearchable);

        Assert.True(map.TryGetField("secret", out var secret));
        Assert.True(secret!.IsHidden);
    }

    [Fact]
    public void Decorator_before_any_field_throws()
    {
        Assert.Throws<InvalidOperationException>(() => SearchMap.ForDocument("x").Searchable());
    }

    [Fact]
    public void Map_drives_the_engine_free_text_over_searchable_fields()
    {
        // La mappa costruita a mano alimenta il motore: il free-text si espande sui campi Searchable via Atlas.
        var map = ProductsMap();
        var executor = new MongoSearchExecutor<BsonDocument>(map, atlasIndex: "test-index");

        // Simula ciò che fa l'extension per il caso Atlas (Search resta e diventa $search).
        var plan = executor.BuildPlan(new SearchRequest { FullTextSearch = "acme", Projection = ["name"] });

        var should = plan.SearchStage!["$search"]["compound"]["should"].AsBsonArray;
        var paths = should.Select(b => b["autocomplete"]["path"].AsString).ToList();
        Assert.Equal(2, paths.Count); // name + description (searchable), NON price/secret
        Assert.Contains("name", paths);
        Assert.Contains("description", paths);
    }
}
