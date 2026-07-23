using Search.Application.Querying;
using Search.Application.Querying.Dynamic;

namespace Search.Application.Config;

public class ProductEntityConfig : ISearchableEntityConfig
{
    public SearchEntity SearchEntity => SearchEntity.RelationalRaw(SearchableEntityNameDict.Product);

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