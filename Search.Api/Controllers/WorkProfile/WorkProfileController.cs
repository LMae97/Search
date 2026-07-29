using Microsoft.AspNetCore.Mvc;
using WeByte.Search.Application.Config;
using WeByte.Search.Application.Querying;
using WeByte.Search.Application.Querying.Authorization;
using WeByte.Search.Application.Querying.Dynamic;

namespace WeByte.Search.Api.Controllers.WorkProfile;

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
    public IActionResult Search(WorkProfileSearchRequestDto request)
    {
        var caller = new SearchCaller(
            SimulatedFieldDefinitionDatabase.DemoSpace,
            new HashSet<Guid> { SearchPermissions.ViewPrice, SearchPermissions.ViewAudit });

        var entityConfig = new WorkProfileEntityConfig();
        var adapterRequest = WorkProfileSearchRequestAdapter.Adapt(request);

        var result = _search.Search(entityConfig, adapterRequest, caller);

        return Ok(new { result });
    }
}