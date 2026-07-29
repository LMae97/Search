using Microsoft.AspNetCore.Mvc;
using Search.Application.Config;
using Search.Application.Querying;
using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;

namespace Search.Api.Controllers.CompensationPlan;

[ApiController]
[Route("compensation-plans")]
public sealed class CompensationPlanController : ControllerBase
{
    private readonly ISearchService _search;

    public CompensationPlanController(ISearchService search)
    {
        _search = search;
    }

    [HttpPost("search")]
    public IActionResult Search(CompensationPlanSearchRequestDto request)
    {
        var caller = new SearchCaller(
            SimulatedFieldDefinitionDatabase.DemoSpace,
            new HashSet<Guid> { SearchPermissions.ViewPrice, SearchPermissions.ViewAudit });

        // TODO: sostituire con l'id dell'utente autenticato reale, se in futuro serve un filtro "own" anche qui.
        var currentUserId = Guid.Empty;

        var searchRequest = CompensationPlanSearchRequestAdapter.Adapt(request, currentUserId);
        var result = _search.Search(new CompensationPlanEntityConfig(), searchRequest, caller);

        return Ok(new { result });
    }
}
