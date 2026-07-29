using WeByte.Search.Api.Dto;
using WeByte.Search.Core;
using WeByte.Search.Core.Filters;

namespace WeByte.Search.Api.Controllers.Contract;

/// <summary>
/// Combina la parte generica (<see cref="BaseSearchRequestDtoAdapter"/>) con i filtri specifici dei
/// contratti (<see cref="ContractBaseFilters"/>): i due insiemi di filtri vanno in AND.
/// </summary>
public static class ContractRequestAdapter
{
    public static SearchRequest Adapt(SearchContractRequestDto req, Guid currentUserId)
    {
        var baseFilters = ContractBaseFilters.GetBaseFilters(req, currentUserId);
        var advancedFilter = BaseSearchRequestDtoAdapter.BuildFilter(req.Options?.Filters);

        var filters = BaseSearchRequestDtoAdapter.Combine(LogicalOperator.And, [advancedFilter, baseFilters]);
        var sort = BaseSearchRequestDtoAdapter.BuildSort(req.Options?.SortBy);
        var page = BaseSearchRequestDtoAdapter.BuildPage(req.Options);

        return new SearchRequest
        {
            FullTextSearch = req.Search,
            Filter = filters,
            Projection = req.Options?.Columns ?? [],
            Sort = sort,
            Page = page
        };
    }
}