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

    public const string CompensationPlan = "compensationPlan";
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

public class CustomerEntityConfig : ISearchableEntityConfig
{
    public SearchEntity SearchEntity => SearchEntity.RelationalRaw(SearchableEntityName.Customer);

    public IReadOnlyList<string> DefaultProjection => [
        "id",
        "firstName",
        "lastName",
        "email"
    ];

    public IReadOnlyList<SortField> DefaultSort => [
        new SortField("name", SortDirection.Ascending) ,
        new SortField("id", SortDirection.Ascending)
    ];
}

public class WorkProfileEntityConfig : ISearchableEntityConfig
{
    public SearchEntity SearchEntity => SearchEntity.RelationalRaw("workprofile");
    public IReadOnlyList<string> DefaultProjection => [
        "id",
        "name",
        "brandId",
        "brandName"
    ];
    public IReadOnlyList<SortField> DefaultSort => [
        new SortField("name", SortDirection.Ascending),
        new SortField("id", SortDirection.Ascending)
    ];
}

public class CompensationPlanEntityConfig : ISearchableEntityConfig
{
    // Store documentale (Mongo): il nome dell'entità è la chiave delle definizioni campi;
    // il mapping verso la collection reale ("CompensationPlan") vive nel MongoCollectionProvider.
    public SearchEntity SearchEntity => SearchEntity.Document(SearchableEntityName.CompensationPlan);

    public IReadOnlyList<string> DefaultProjection => [
        "id",
        "name",
        "description",
        "createdAt"
    ];

    public IReadOnlyList<SortField> DefaultSort => [
        new SortField("name", SortDirection.Ascending),
        new SortField("id", SortDirection.Ascending)
    ];
}

public class UserEntityConfig : ISearchableEntityConfig
{
    public SearchEntity SearchEntity => SearchEntity.RelationalRaw("utente");
    public IReadOnlyList<string> DefaultProjection => [
        "id",
        "username",
        "email"
    ];
    public IReadOnlyList<SortField> DefaultSort => [
        new SortField("username", SortDirection.Ascending),
        new SortField("id", SortDirection.Ascending)
    ];
}