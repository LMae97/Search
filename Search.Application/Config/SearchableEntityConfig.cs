using Search.Application.Querying;
using Search.Application.Querying.Dynamic;

namespace Search.Application.Config;

public interface ISearchableEntityConfig
{
    SearchEntity SearchEntity { get; }
    IReadOnlyList<string> DefaultProjection { get; }
    IReadOnlyList<SortField> DefaultSort { get; }

}

public static class SearchableEntityName
{
    public const string Product = "product";
    public const string Brand = "brand";

    public const string Customer = "customer";
}

public class ProductEntityConfig : ISearchableEntityConfig
{
    public SearchEntity SearchEntity => SearchEntity.RelationalRaw(SearchableEntityName.Product);

    public IReadOnlyList<string> DefaultProjection => [
        "id", 
        "name", 
        "description", 
        "status"
    ];

    public IReadOnlyList<SortField> DefaultSort => [ 
        new SortField("name", SortDirection.Ascending) ,
        new SortField("id", SortDirection.Ascending)
    ];
}

public class BrandEntityConfig : ISearchableEntityConfig
{
    public SearchEntity SearchEntity => SearchEntity.RelationalRaw(SearchableEntityName.Brand);

    public IReadOnlyList<string> DefaultProjection => [
        "id",
        "name"
    ];

    public IReadOnlyList<SortField> DefaultSort => [
        new SortField("name", SortDirection.Ascending),
        new SortField("id", SortDirection.Ascending)
    ];
}