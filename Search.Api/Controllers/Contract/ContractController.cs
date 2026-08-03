using Microsoft.AspNetCore.Mvc;
using WeByte.Search.Application.Config;
using WeByte.Search.Application.Querying.Authorization;
using WeByte.Search.Application.Querying.Dynamic;
using WeByte.Search.Application.Search;

namespace WeByte.Search.Api.Controllers.Contract;

[ApiController]
[Route("contracts")]
public sealed class ContractController : ControllerBase
{
    private readonly ISearchService _search;

    public ContractController(ISearchService search)
    {
        _search = search;
    }

    [HttpPost("search")]
    public IActionResult Search(SearchContractRequestDto request)
    {
        var caller = new SearchCaller(
            SimulatedFieldDefinitionDatabase.DemoSpace,
            new HashSet<Guid> { SearchPermissions.ViewPrice, SearchPermissions.ViewAudit });

        // TODO: sostituire con l'id dell'utente autenticato reale (serve per Assignees.OwnOption).
        var currentUserId = Guid.Empty;

        var searchRequest = ContractRequestAdapter.Adapt(request, currentUserId);
        var result = _search.SearchWithCount(new ContractEntityConfig(), searchRequest, caller);

        return Ok(new { result });
    }
}
