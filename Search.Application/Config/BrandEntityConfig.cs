using Search.Core;
using Search.Core.Dynamic;
using Search.Core.Filters;

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

    public string IdField => "id";

    public FilterNode? AuthFilters(IVisibilityFilters? filters) => null;

    public IReadOnlyList<string> HiddenProjection => [
        IdField
    ];
}