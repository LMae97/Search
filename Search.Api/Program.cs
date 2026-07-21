using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Search.Application.Querying;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Validation;
using Search.Api.Serialization;
using Search.Infrastructure.Sql;

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
// Mongo: da abilitare quando l'API avrà una connessione Mongo (+ riferimento a Search.Infrastructure.Mongo). Servono:
//   builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
//   builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase("<db>"));
//   builder.Services.AddSingleton<IMongoCollectionProvider>(sp => new MongoCollectionProvider(
//       sp.GetRequiredService<IMongoDatabase>(), new Dictionary<string, string> { ["order"] = "orders" }));
//   builder.Services.AddSingleton<ISearchHandler, MongoSearchHandler>();
builder.Services.AddSingleton<ISearchService, SearchService>();

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