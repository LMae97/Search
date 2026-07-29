using WeByte.Search.Core;
using WeByte.Search.Core.Dynamic;
using WeByte.Search.Core.Filters;

namespace WeByte.Search.Application.Config;

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

    public string IdField => "id";
    public FilterNode? AuthFilters(IVisibilityFilters? filters) => null;

    public IReadOnlyList<string> HiddenProjection => [
        IdField
    ];
}

public interface IVisibilityFilters; //Marker