using Microsoft.AspNetCore.Mvc;
using WeByte.Search.Application.Config;
using WeByte.Search.Application.Querying.Authorization;
using WeByte.Search.Application.Querying.Dynamic;
using WeByte.Search.Application.Search;

namespace WeByte.Search.Api.Controllers.CompensationPlan;

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
        var result = _search.SearchWithCount(new CompensationPlanEntityConfig(), searchRequest, caller);

        return Ok(new { result });
    }
}
