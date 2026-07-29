using WeByte.Search.Api.Dto;
using WeByte.Search.Core;
using WeByte.Search.Core.Filters;

namespace WeByte.Search.Api.Controllers.User;

public class UserSearchRequestDtoAdapter
{
    public static SearchRequest Adapt(UserSearchRequestDto req)
    {
        var baseFilters = UserBaseFilters.GetBaseFilters(req);
        var advancedFilter = BaseSearchRequestDtoAdapter.BuildFilter(req.Options?.Filters);

        var filters = BaseSearchRequestDtoAdapter.Combine(LogicalOperator.And, [advancedFilter, baseFilters]);
        var sort = BaseSearchRequestDtoAdapter.BuildSort(req.Options?.SortBy);
        var page = BaseSearchRequestDtoAdapter.BuildPage(req.Options);

        return new SearchRequest
        {
            FullTextSearch = null, // User è su Postgres: nessun full-text nativo per ora
            Filter = filters,
            Projection = req.Options?.Columns ?? [],
            Sort = sort,
            Page = page
        };
    }

    public static class UserBaseFilters
    {
        public static FilterNode? GetBaseFilters(UserSearchRequestDto req)
        {
            var srcParam = req?.Search;

            var filters = new List<FilterNode>();

            if (!string.IsNullOrEmpty(srcParam))
            {
                filters.Add(Filter.Or(
                    Filter.Contains("username", srcParam),
                    Filter.Contains("fullName", srcParam),
                    Filter.Contains("email", srcParam),
                    Filter.Contains("phone", srcParam)
                ));
            }

            BaseSearchRequestDtoAdapter.AddIn(filters, "organizationId", req?.Organizations?.Select(id => (object?)id));
            BaseSearchRequestDtoAdapter.AddIn(filters, "brandIds", req?.Brands?.Select(id => (object?)id));
            BaseSearchRequestDtoAdapter.AddIn(filters, "workProfileIds", req?.WorkProfiles?.Select(id => (object?)id));
            BaseSearchRequestDtoAdapter.AddIn(filters, "roleIds", req?.Roles?.Select(id => (object?)id));
            BaseSearchRequestDtoAdapter.AddIn(filters, "typologyId", req?.Types?.Select(id => (object?)id));
            BaseSearchRequestDtoAdapter.AddIn(filters, "tagIds", req?.Tags?.Select(id => (object?)id));

            AddIsActiveTriState(filters, req?.IsActive);

            return filters.Count > 0 ? Filter.And([.. filters]) : null;
        }

        // Tri-stato come "Paid" sui contratti: solo attivi / solo non attivi / entrambi selezionati = nessun filtro.
        private static void AddIsActiveTriState(List<FilterNode> filters, List<bool>? isActive)
        {
            if (isActive is not { Count: > 0 }) return;

            var includeActive = isActive.Contains(true);
            var includeInactive = isActive.Contains(false);
            if (includeActive && includeInactive) return;

            filters.Add(Filter.Eq("isActive", includeActive));
        }
    }
}
