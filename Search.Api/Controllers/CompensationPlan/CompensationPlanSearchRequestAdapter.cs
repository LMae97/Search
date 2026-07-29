using Search.Api.Dto;
using Search.Core;
using Search.Core.Filters;

namespace Search.Api.Controllers.CompensationPlan;

public class CompensationPlanSearchRequestAdapter
{
    public static SearchRequest Adapt(CompensationPlanSearchRequestDto req, Guid currentUserId)
    {
        var baseFilters = CompensationPlanBaseFilters.GetBaseFilters(req);
        var advancedFilter = BaseSearchRequestDtoAdapter.BuildFilter(req.Options?.Filters);

        var filters = BaseSearchRequestDtoAdapter.Combine(LogicalOperator.And, [advancedFilter, baseFilters]);
        var sort = BaseSearchRequestDtoAdapter.BuildSort(req.Options?.SortBy);
        var page = BaseSearchRequestDtoAdapter.BuildPage(req.Options);

        return new SearchRequest
        {
            FullTextSearch = null,
            Filter = filters,
            Projection = req.Options?.Columns ?? [],
            Sort = sort,
            Page = page
        };
    }

    public static class CompensationPlanBaseFilters
    {
        public static FilterNode? GetBaseFilters(CompensationPlanSearchRequestDto req)
        {
            var srcParam = req?.Search;
            var brandIds = req?.Brands?.Select(id => (object?)id);
            var wpIds = req?.WorkProfiles?.Select(id => (object?)id);

            var filters = new List<FilterNode>();

            if (!string.IsNullOrEmpty(srcParam))
            {
                filters.Add(Filter.Or(
                    Filter.Contains("name", srcParam),
                    Filter.Contains("brandName", srcParam)
                ));
            }

            BaseSearchRequestDtoAdapter.AddIn(filters, "brandId", brandIds);
            BaseSearchRequestDtoAdapter.AddIn(filters, "workProfileId", wpIds);

            //TODO: FARE VALIDITY

            return filters.Count > 0 ? Filter.And([.. filters]) : null;
        }
    }
}
