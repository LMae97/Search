using Microsoft.AspNetCore.Mvc;
using WeByte.Search.Api.Export;
using WeByte.Search.Application.Config;
using WeByte.Search.Application.Export;
using WeByte.Search.Application.Querying.Authorization;
using WeByte.Search.Application.Querying.Dynamic;
using WeByte.Search.Application.Search;
using WeByte.Search.Export;

namespace WeByte.Search.Api.Controllers.WorkProfile;

[ApiController]
[Route("work-profiles")]
public sealed class WorkProfileController : ControllerBase
{
    // Utente di audit fittizio finché non c'è l'autenticazione reale.
    private const string AuditUser = "api@we-byte.it";

    private readonly ISearchService _search;
    private readonly SearchExporter _exporter;
    private readonly ExportFormats _formats;
    private readonly IExportStore _exportStore;

    public WorkProfileController(
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
    public IActionResult Search(WorkProfileSearchRequestDto request)
    {
        var caller = new SearchCaller(
            SimulatedFieldDefinitionDatabase.DemoSpace,
            new HashSet<Guid> { SearchPermissions.ViewPrice, SearchPermissions.ViewAudit });

        var entityConfig = new WorkProfileEntityConfig();
        var adapterRequest = WorkProfileSearchRequestAdapter.Adapt(request);

        var result = _search.SearchWithCount(entityConfig, adapterRequest, caller);

        return Ok(new { result });
    }

    /// <summary>
    /// Stessa richiesta di <see cref="Search"/>, ma produce un file di export e restituisce dove si trova
    /// (come il legacy, che tornava una URI e non i byte). Solo le colonne visibili finiscono nel file.
    /// <para>
    /// Differenza di semantica rispetto alla ricerca: <c>pageSize</c> non è la dimensione di una pagina ma
    /// il <b>numero massimo di righe da esportare</b> (assente = tutte). Le pagine effettive le decide il
    /// pager in base al budget di celle. È la stessa convenzione del vecchio <c>ExportCsvAsync</c>.
    /// </para>
    /// </summary>
    /// <param name="format"><c>csv</c> (default) oppure <c>xlsx</c>.</param>
    [HttpPost("export")]
    public IActionResult Export(WorkProfileSearchRequestDto request, [FromQuery] string format = "csv")
    {
        if (!_formats.TryGet(format, out var writerFactory))
            return BadRequest(new { detail = $"Formato '{format}' non supportato. Disponibili: {_formats.Available}." });

        var caller = new SearchCaller(
            SimulatedFieldDefinitionDatabase.DemoSpace,
            new HashSet<Guid> { SearchPermissions.ViewPrice, SearchPermissions.ViewAudit });

        var entityConfig = new WorkProfileEntityConfig();
        var adapterRequest = WorkProfileSearchRequestAdapter.Adapt(request);
        var paging = new ExportPagingOptions { MaxRows = request.Options?.PageSize };

        // Prima di scrivere: rientra nella soglia sincrona? Questo progetto si ferma qui — non implementa
        // la coda/funzione esterna per il ramo "troppo grande" (è responsabilità dell'applicazione che lo
        // ospita, come acquario-be), quindi oggi risponde solo 202 con la diagnostica.
        var decision = _exporter.Decide(entityConfig, adapterRequest, caller);
        if (decision is ExportDecision.TooLargeForSync tooLarge)
        {
            return Accepted(new
            {
                message = "Export troppo grande per essere generato in sincrono.",
                rowCount = tooLarge.RowCount,
                maxSyncRows = tooLarge.MaxSyncRows
            });
        }

        var fileName = $"work-profiles_{DateTime.Now:yyyyMMdd_HHmmss}.{writerFactory.FileExtension}";

        // Lo store apre il file e ci scriviamo dentro direttamente: nessun MemoryStream di mezzo, quindi il
        // CSV va su disco un batch alla volta.
        var stored = _exportStore.Save(
            fileName,
            stream => _exporter.Write(stream, writerFactory, entityConfig, adapterRequest, caller, paging));

        return Ok(stored);
    }
}