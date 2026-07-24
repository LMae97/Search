using Search.Application.Querying;

namespace Search.Api.Contract;

/// <summary>
/// Adatta il DTO del FE alla <see cref="SearchRequest"/> del motore. Il free-text del FE finisce in
/// <see cref="SearchRequest.Search"/>: è poi la config d'entità (Contract → Atlas) a deciderne la traduzione,
/// non l'adapter. Niente più campo-filtro finto tipo "_specialsearch".
/// </summary>
public class ContractSearchRequestAdapter
{
    public static SearchRequest Adapt(ContractSearchRequest req) => new()
    {
        Search = req.Search,
        Filter = req.Filter,
        Projection = req.Projection,
        Sort = req.Sort,
        Page = req.Page
    };
}
