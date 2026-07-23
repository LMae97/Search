using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Metadata;
using static Search.Tests.TestSupport;

namespace Search.Tests;

/// <summary>Autorizzazione: "niente permesso ⇒ campo assente" dalla mappa effettiva (whitelist di sicurezza).</summary>
public sealed class EffectiveSearchMapTests
{
    private const string E = "product";
    private static readonly Guid ViewPrice = SearchPermissions.ViewPrice;

    private static IEntitySearchMap MapFor(SearchCaller caller) => Map(
        SearchEntity.RelationalRaw(E), 
        caller,
        Def(E, "name", FieldKind.String, "\"p\".\"Name\""),
        Def(E, "price", FieldKind.Decimal, "\"p\".\"Price\"", permission: ViewPrice));

    [Fact]
    public void Field_requiring_permission_is_hidden_when_caller_lacks_it()
    {
        var map = MapFor(Caller()); // nessun permesso

        Assert.True(map.TryGetField("name", out _));
        Assert.False(map.TryGetField("price", out _));
    }

    [Fact]
    public void Field_requiring_permission_is_visible_when_caller_has_it()
    {
        var map = MapFor(Caller(ViewPrice));

        Assert.True(map.TryGetField("price", out var price));
        Assert.Equal("price", price!.Name);
        Assert.Equal("\"p\".\"Price\"", price.StoragePath);
        Assert.Equal(FieldKind.Decimal, price.Kind);
    }
}
