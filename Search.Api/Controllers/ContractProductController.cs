using Microsoft.AspNetCore.Mvc;
using Search.Api.Contract;
using Search.Application.Config;
using Search.Application.Querying;
using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;

namespace Search.Api.Controllers;

/// <summary>
/// Ricerca "a livello di prodotto" (ogni prodotto di un contratto è una riga indipendente) — equivalente al
/// vecchio contesto <c>ContractProducts</c>. Stessi DTO/adapter di <see cref="ContractController"/>: cambia
/// solo la config (<see cref="ContractProductEntityConfig"/>, che attiva l'unwind Mongo).
/// </summary>
[ApiController]
[Route("contract-products")]
public sealed class ContractProductController : ControllerBase
{
    private readonly ISearchService _search;

    public ContractProductController(ISearchService search)
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
        var result = _search.Search(new ContractProductEntityConfig(), searchRequest, caller);

        return Ok(new { result });
    }
}
