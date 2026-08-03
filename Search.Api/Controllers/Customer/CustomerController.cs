using Microsoft.AspNetCore.Mvc;
using WeByte.Search.Api.Controllers.WorkProfile;
using WeByte.Search.Api.Export;
using WeByte.Search.Export;
using WeByte.Search.Application.Config;
using WeByte.Search.Application.Export;
using WeByte.Search.Application.Querying.Authorization;
using WeByte.Search.Application.Querying.Dynamic;
using WeByte.Search.Application.Search;

namespace WeByte.Search.Api.Controllers.Customer;

[ApiController]
[Route("customers")]
public sealed class CustomerController : ControllerBase
{
    // Utente di audit fittizio finché non c'è l'autenticazione reale.
    private const string AuditUser = "api@we-byte.it";

    private readonly ISearchService _search;
    private readonly SearchExporter _exporter;
    private readonly ExportFormats _formats;
    private readonly IExportStore _exportStore;

    public CustomerController(
        ISearchService search,
        SearchExporter exporter,
        ExportFormats formats,
        IExportStore exportStore)
    {
        _search = search;
        _exporter = exporter;
        _formats = formats;
        _exportStore = exportStore;
    }

    [HttpPost("search")]
    public IActionResult Search(CustomerSearchRequestDto request)
    {
        var caller = new SearchCaller(
            SimulatedFieldDefinitionDatabase.DemoSpace,
            new HashSet<Guid> { SearchPermissions.ViewPrice, SearchPermissions.ViewAudit });

        var entityConfig = new CustomerEntityConfig();
        var searchRequest = CustomerSearchRequestAdapter.Adapt(request);

        var result = _search.SearchWithCount(entityConfig, searchRequest, caller);

        return Ok(new { result });
    }

    /// <param name="format"><c>csv</c> (default) oppure <c>xlsx</c>.</param>
    [HttpPost("export")]
    public IActionResult Export(CustomerSearchRequestDto request, [FromQuery] string format = "csv")
    {
        if (!_formats.TryGet(format, out var writerFactory))
            return BadRequest(new { detail = $"Formato '{format}' non supportato. Disponibili: {_formats.Available}." });

        var caller = new SearchCaller(
            SimulatedFieldDefinitionDatabase.DemoSpace,
            new HashSet<Guid> { SearchPermissions.ViewPrice, SearchPermissions.ViewAudit });

        var entityConfig = new CustomerEntityConfig();
        var adapterRequest = CustomerSearchRequestAdapter.Adapt(request);

        // pageSize qui è il tetto di righe totali, non la dimensione di una pagina: le pagine le decide il
        // pager in base al budget di celle.
        var paging = new ExportPagingOptions { MaxRows = request.Options?.PageSize };

        var fileName = $"customers_{DateTime.Now:yyyyMMdd_HHmmss}.{writerFactory.FileExtension}";

        var stored = _exportStore.Save(
            fileName,
            stream => _exporter.Write(stream, writerFactory, entityConfig, adapterRequest, caller, paging));

        return Ok(stored);
    }
}