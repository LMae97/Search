using Microsoft.AspNetCore.Mvc;
using WeByte.Search.Application.Config;
using WeByte.Search.Application.Querying.Authorization;
using WeByte.Search.Application.Querying.Dynamic;
using WeByte.Search.Application.Search;

namespace WeByte.Search.Api.Controllers.User;

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

        var result = _search.SearchWithCount(entityConfig, searchRequest, caller);

        return Ok(new { result });
    }
}