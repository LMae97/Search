using Search.Application.Querying;
using Search.Application.Querying.Filters;

namespace Search.Api.Controllers.WorkProfile;

public class WorkProfileBaseFilterResolver
{
    public static FilterNode? GetBaseFilters(WorkProfileSearchRequest request)
    {
        var srcParam = request?.Search;

        var filters = new List<FilterNode>();

        if (!string.IsNullOrEmpty(srcParam)) {
            filters.Add(Filter.Or(
                Filter.Contains("name", srcParam),
                Filter.Contains("brandName", srcParam)
            ));
        }
        
        return filters.Count > 0 ? Filter.And([.. filters]) : null;
    }
}

public class WorkProfileSearchRequestAdapter
{
    public static SearchRequest Adapt(WorkProfileSearchRequest req)
    {
        var combinedFilter = WorkProfileBaseFilterResolver.GetBaseFilters(req);

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
}