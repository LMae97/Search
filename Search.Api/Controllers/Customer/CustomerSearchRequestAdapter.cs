using WeByte.Search.Api.Dto;
using WeByte.Search.Core;
using WeByte.Search.Core.Filters;

namespace WeByte.Search.Api.Controllers.Customer;

public class CustomerSearchRequestAdapter
{
    public static SearchRequest Adapt(CustomerSearchRequestDto req)
    {
        var baseFilters = CustomerBaseFilters.GetBaseFilters(req);
        var advancedFilter = BaseSearchRequestDtoAdapter.BuildFilter(req.Options?.Filters);

        var filters = BaseSearchRequestDtoAdapter.Combine(LogicalOperator.And, [advancedFilter, baseFilters]);
        var sort = BaseSearchRequestDtoAdapter.BuildSort(req.Options?.SortBy);
        var page = BaseSearchRequestDtoAdapter.BuildPage(req.Options);

        return new SearchRequest
        {
            FullTextSearch = null, // Customer è su Postgres: nessun full-text nativo per ora (vedi nota in fondo al file)
            Filter = filters,
            Projection = req.Options?.Columns ?? [],
            Sort = sort,
            Page = page
        };
    }

    public static class CustomerBaseFilters
    {
        public static FilterNode? GetBaseFilters(CustomerSearchRequestDto req)
        {
            var srcParam = req?.Search;
            // "tagIds" è FieldKind.String nello schema attuale (non Guid): stringify.
            var tagIds = req?.Tags?.Select(id => (object?)id.ToString());

            var filters = new List<FilterNode>();

            if (!string.IsNullOrEmpty(srcParam))
            {
                filters.Add(Filter.Or(
                    Filter.Contains("businessName", srcParam),
                    Filter.Contains("firstName", srcParam),
                    Filter.Contains("lastName", srcParam),
                    Filter.Contains("email", srcParam)
                ));
            }

            BaseSearchRequestDtoAdapter.AddIn(filters, "tagIds", tagIds);

            return filters.Count > 0 ? Filter.And([.. filters]) : null;
        }
    }
}
