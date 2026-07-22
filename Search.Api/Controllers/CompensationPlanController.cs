using Microsoft.AspNetCore.Mvc;
using Search.Application.Config;
using Search.Application.Querying;
using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;

namespace Search.Api.Controllers;

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
    public IActionResult Search(SearchRequest request)
    {
        var caller = new SearchCaller(
            SimulatedFieldDefinitionDatabase.DemoSpace,
            new HashSet<Guid> { SearchPermissions.ViewPrice, SearchPermissions.ViewAudit });

        var result = _search.Search(new CompensationPlanEntityConfig(), request, caller);

        return Ok(new { result.Items, result.TotalCount, result.PageNumber, result.PageSize });
    }
}
