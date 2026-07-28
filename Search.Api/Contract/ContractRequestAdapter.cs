using Search.Api.Dto;
using Search.Core;
using Search.Core.Filters;

namespace Search.Api.Contract;

/// <summary>
/// Combina la parte generica (<see cref="BaseSearchRequestDtoAdapter"/>) con i filtri specifici dei
/// contratti (<see cref="ContractBaseFilters"/>): i due insiemi di filtri vanno in AND.
/// </summary>
public static class ContractRequestAdapter
{
    public static SearchRequest Adapt(SearchContractRequestDto req, Guid currentUserId)
    {
        var request = BaseSearchRequestDtoAdapter.ToSearchRequest(req);
        var extra = ContractBaseFilters.GetBaseFilters(req, currentUserId);

        var filter = BaseSearchRequestDtoAdapter.Combine(LogicalOperator.And, [request.Filter, extra]);

        return new SearchRequest
        {
            Search = request.Search,
            Filter = filter,
            Projection = request.Projection,
            Sort = request.Sort,
            Page = request.Page
        };
    }
}
