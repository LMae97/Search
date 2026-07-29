using WeByte.Search.Api.Dto;
using WeByte.Search.Core;
using WeByte.Search.Core.Filters;

namespace WeByte.Search.Api.Controllers.WorkProfile;

public class WorkProfileSearchRequestAdapter
{
    public static SearchRequest Adapt(WorkProfileSearchRequestDto req)
    {
        var baseFilters = WorkProfileBaseFilters.GetBaseFilters(req);
        var advancedFilter = BaseSearchRequestDtoAdapter.BuildFilter(req.Options?.Filters);

        var filters = BaseSearchRequestDtoAdapter.Combine(LogicalOperator.And, [advancedFilter, baseFilters]);
        var sort = BaseSearchRequestDtoAdapter.BuildSort(req.Options?.SortBy);
        var page = BaseSearchRequestDtoAdapter.BuildPage(req.Options);

        return new SearchRequest
        {
            FullTextSearch = null,  //viene usata solo per mongo
            Filter = filters,
            Projection = req.Options?.Columns ?? [],
            Sort = sort,
            Page = page
        };
    }
}

public static class WorkProfileBaseFilters
{
    public static FilterNode? GetBaseFilters(WorkProfileSearchRequestDto request)
    {
        var srcParam = request?.Search;

        var filters = new List<FilterNode>();

        if (!string.IsNullOrEmpty(srcParam))
        {
            filters.Add(Filter.Or(
                Filter.Contains("name", srcParam),
                Filter.Contains("brandName", srcParam)
            ));
        }

        return filters.Count > 0 ? Filter.And([.. filters]) : null;
    }
}