using Microsoft.AspNetCore.Mvc;
using WeByte.Search.Application.Config;
using WeByte.Search.Application.Querying;
using WeByte.Search.Application.Querying.Authorization;
using WeByte.Search.Application.Querying.Dynamic;

namespace WeByte.Search.Api.Controllers.Customer;

[ApiController]
[Route("customers")]
public sealed class CustomerController : ControllerBase
{
    // Utente di audit fittizio finché non c'è l'autenticazione reale.
    private const string AuditUser = "api@we-byte.it";

    private readonly ISearchService _search;

    public CustomerController(ISearchService search)
    {
        _search = search;
    }

    [HttpPost("search")]
    public IActionResult Search(CustomerSearchRequestDto request)
    {
        var caller = new SearchCaller(
            SimulatedFieldDefinitionDatabase.DemoSpace,
            new HashSet<Guid> { SearchPermissions.ViewPrice, SearchPermissions.ViewAudit });

        var entityConfig = new CustomerEntityConfig();
        var searchRequest = CustomerSearchRequestAdapter.Adapt(request);

        var result = _search.Search(entityConfig, searchRequest, caller);

        return Ok(new { result });
    }
}