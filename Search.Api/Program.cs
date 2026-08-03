using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using WeByte.Search.Api.Export;
using WeByte.Search.Application.Export;
using WeByte.Search.Export;
using WeByte.Search.Export.Xlsx;
using WeByte.Search.Application.Querying.Dynamic;
using WeByte.Search.Api.Serialization;
using WeByte.Search.Infrastructure.Sql;
using WeByte.Search.Infrastructure.Mongo;
using WeByte.Search.Sql;
using WeByte.Search.Core.Dynamic;
using MongoDB.Driver;
using WeByte.Search.Core.Validation;
using WeByte.Search.Application.Search;

var builder = WebApplication.CreateBuilder(args);

// Controller + JSON: converter polimorfico per l'albero di filtri + enum come stringhe.
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new FilterNodeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// --- Postgres: EF per il CRUD, connessione ADO.NET per la ricerca SQL grezza (stesso DB) ---
var catalogConnectionString = builder.Configuration.GetConnectionString("Catalog")
    ?? throw new InvalidOperationException("Manca la connection string 'Catalog' (appsettings o variabile d'ambiente).");

builder.Services.AddSingleton<ICatalogConnectionFactory>(new NpgsqlCatalogConnectionFactory(catalogConnectionString)); // ricerca raw
builder.Services.AddSingleton<ISqlSchemaProvider, CatalogSqlSchemaProvider>();

builder.Services.AddSingleton<ISearchFieldDefinitionProvider>(_ => SimulatedFieldDefinitionDatabase.Create());
builder.Services.AddSingleton<DbBackedSearchMapProvider>();

// --- Layer di ricerca: un handler per STORE, dietro un facade unico (ISearchService) ---
// Il facade smista per StoreKind (dal SearchEntityRegistry): un handler serve tutte le entità del suo store.
builder.Services.AddSingleton<ISearchHandler, SqlSearchHandler>();   // copre tutte le entità PostgresRaw (product, brand, …)

// Mongo: opt-in. Registrato solo se c'è la connection string, così un run solo-SQL non richiede un Mongo attivo.
// L'handler Mongo copre TUTTE le entità documentali (compensationPlan, …); il mapping entità→collection sta nel provider.
var mongoConnectionString = builder.Configuration.GetConnectionString("Mongo");
if (!string.IsNullOrWhiteSpace(mongoConnectionString))
{
    var mongoDatabaseName = builder.Configuration.GetValue<string>("Mongo:Database");

    builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
    builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoDatabaseName));
    builder.Services.AddSingleton<IMongoCollectionProvider>(sp => new MongoCollectionProvider(
        sp.GetRequiredService<IMongoDatabase>(),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["compensationPlan"] = "CompensationPlan",
            ["contract"] = "Contract"
        }));
    builder.Services.AddSingleton<ISearchHandler, MongoSearchHandler>();
}

builder.Services.AddSingleton<ISearchService, SearchService>();

// Export: il servizio è indipendente dal formato; i formati sono le fabbriche registrate qui sotto.
// Questo è l'unico punto che conosce sia SearchWithCount.Export sia SearchWithCount.Export.Xlsx.
builder.Services.AddSingleton<SearchExporter>();

// Preset italiano (07/12/2024 12:29): dd/MM/yyyy, culture it-IT. Vedi ExportValueFormat.Italian per il
// perché delle due sintassi separate (stringa .NET per il CSV, number format Excel per l'xlsx).
builder.Services.AddSingleton<ITabularWriterFactory>(_ =>
    new CsvTabularWriterFactory(new CsvExportOptions { Values = ExportValueFormat.Italian }));

builder.Services.AddSingleton<ITabularWriterFactory>(_ =>
    new XlsxTabularWriterFactory(XlsxExportOptions.Italian));

builder.Services.AddSingleton<ExportFormats>();

// Destinazione dei file prodotti. In locale una cartella del progetto; in produzione al suo posto va uno
// store su blob con SAS a scadenza (il "temporary-file-export" del vecchio sistema), stessa interfaccia.
// Il percorso è ancorato alla ContentRoot, così non dipende dalla directory da cui si lancia il processo.
var exportDirectory = builder.Configuration.GetValue<string>("Export:Directory")
    ?? Path.Combine(builder.Environment.ContentRootPath, "temp_file");

builder.Services.AddSingleton<IExportStore>(_ => new LocalDirectoryExportStore(exportDirectory));

var app = builder.Build();

// Traduzione delle eccezioni di dominio/validazione in 4xx (invece di 500).
// Questo è il middleware delle eccezioni
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (SearchValidationException ex)
    {
        await WriteProblem(context, StatusCodes.Status422UnprocessableEntity, ex.Message);
    }
    catch (ArgumentException ex)
    {
        await WriteProblem(context, StatusCodes.Status400BadRequest, ex.Message);
    }
});

app.MapControllers();

app.Run();

// === Helper: eccezioni → problema JSON, e seed iniziale ===

static Task WriteProblem(HttpContext context, int status, string detail)
{
    context.Response.StatusCode = status;
    return context.Response.WriteAsJsonAsync(new { status, detail });
}