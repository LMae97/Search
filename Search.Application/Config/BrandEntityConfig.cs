using Search.Application.Querying;
using Search.Application.Querying.Dynamic;

namespace Search.Application.Config;

public class BrandEntityConfig : ISearchableEntityConfig
{
    public SearchEntity SearchEntity => SearchEntity.RelationalRaw(SearchableEntityNameDict.Brand);

    public IReadOnlyList<string> DefaultProjection => [
        "id",
        "name"
    ];

    public IReadOnlyList<SortField> DefaultSort => [
        new SortField("name", SortDirection.Ascending),
        new SortField("id", SortDirection.Ascending)
    ];
}
