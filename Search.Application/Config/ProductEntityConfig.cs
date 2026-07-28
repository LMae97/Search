using Search.Core;
using Search.Core.Dynamic;
using Search.Core.Filters;

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

    public string IdField => "id";
    public FilterNode? AuthFilters(IVisibilityFilters? filters) => null;

    public IReadOnlyList<string> HiddenProjection => [
        IdField
    ];
}