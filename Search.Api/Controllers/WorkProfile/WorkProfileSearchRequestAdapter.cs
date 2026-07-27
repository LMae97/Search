using Search.Core;
using Search.Core.Filters;

namespace Search.Api.Controllers.WorkProfile;

public class WorkProfileSearchRequestAdapter
{
    public static SearchRequest Adapt(WorkProfileSearchRequest req)
    {
        var combinedFilter = GetBaseFilters(req);

        if (combinedFilter != null && req.Filter != null)
        {
            combinedFilter = Filter.And(combinedFilter, req.Filter);
        }
        
        combinedFilter ??= req.Filter;

        return new SearchRequest
        {
            Filter = combinedFilter,
            Projection = req.Projection,
            Sort = req.Sort,
            Page = req.Page
        };
    }

    private static FilterNode? GetBaseFilters(WorkProfileSearchRequest request)
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