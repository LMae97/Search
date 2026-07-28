using Microsoft.AspNetCore.Mvc;
using Search.Api.Contract;
using Search.Application.Config;
using Search.Application.Querying;
using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;

namespace Search.Api.Controllers;

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
        var result = _search.Search(new ContractEntityConfig(), searchRequest, caller);

        return Ok(new { result });
    }
}
