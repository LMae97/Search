using Microsoft.AspNetCore.Mvc;
using Search.Application.Config;
using Search.Application.Querying;
using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;

namespace Search.Api.Controllers.WorkProfile;

[ApiController]
[Route("work-profiles")]
public sealed class WorkProfileController : ControllerBase
{
    // Utente di audit fittizio finché non c'è l'autenticazione reale.
    private const string AuditUser = "api@we-byte.it";

    private readonly ISearchService _search;

    public WorkProfileController(ISearchService search)
    {
        _search = search;
    }

    [HttpPost("search")]
    public IActionResult Search(WorkProfileSearchRequest request)
    {
        var caller = new SearchCaller(
            SimulatedFieldDefinitionDatabase.DemoSpace,
            new HashSet<Guid> { SearchPermissions.ViewPrice, SearchPermissions.ViewAudit });

        var entityConfig = new WorkProfileEntityConfig();
        var adapterRequest = WorkProfileSearchRequestAdapter.Adapt(request);

        var result = _search.Search(entityConfig, adapterRequest, caller);

        return Ok(new { result.Items, result.TotalCount, result.PageNumber, result.PageSize });
    }
}