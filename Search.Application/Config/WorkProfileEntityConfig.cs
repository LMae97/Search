using Search.Application.Querying;
using Search.Application.Querying.Dynamic;

namespace Search.Application.Config;

public class WorkProfileEntityConfig : ISearchableEntityConfig
{
    public SearchEntity SearchEntity => SearchEntity.RelationalRaw(SearchableEntityNameDict.WorkProfile);
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