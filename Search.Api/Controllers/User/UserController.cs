using Microsoft.AspNetCore.Mvc;
using Search.Application.Config;
using Search.Application.Querying;
using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;

namespace Search.Api.Controllers.User;

[ApiController]
[Route("users")]
public sealed class UserController : ControllerBase
{
    // Utente di audit fittizio finché non c'è l'autenticazione reale.
    private const string AuditUser = "api@we-byte.it";

    private readonly ISearchService _search;

    public UserController(ISearchService search)
    {
        _search = search;
    }

    [HttpPost("search")]
    public IActionResult Search(UserSearchRequestDto request)
    {
        var caller = new SearchCaller(
            SimulatedFieldDefinitionDatabase.DemoSpace,
            new HashSet<Guid> { SearchPermissions.ViewPrice, SearchPermissions.ViewAudit });

        var entityConfig = new UserEntityConfig();
        var searchRequest = UserSearchRequestDtoAdapter.Adapt(request);

        var result = _search.Search(entityConfig, searchRequest, caller);

        return Ok(new { result });
    }
}