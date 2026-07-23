using Search.Application.Querying;
using Search.Application.Querying.Dynamic;

namespace Search.Application.Config;

public class CustomerEntityConfig : ISearchableEntityConfig
{
    public SearchEntity SearchEntity => SearchEntity.RelationalRaw(SearchableEntityNameDict.Customer);

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