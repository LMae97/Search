using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Search.Application.Catalog;
using Search.Application.Querying;
using Search.Application.Querying.Dynamic;
using Search.Domain.Common;
using Search.Application.Querying.Validation;
using Search.Api.Serialization;
using Search.Infrastructure.Sql;
using Search.Infrastructure.Sql.EF;

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

builder.Services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(catalogConnectionString));
builder.Services.AddScoped<IProductRepository, EfProductRepository>();                              // CRUD via EF
builder.Services.AddSingleton<ICatalogConnectionFactory>(new NpgsqlCatalogConnectionFactory(catalogConnectionString)); // ricerca raw
builder.Services.AddSingleton<ISqlSchemaProvider, CatalogSqlSchemaProvider>();

// --- Motore di ricerca (definizioni "a DB"): product = SQL grezzo (PostgresRaw), order = Mongo ---
builder.Services.AddSingleton(new SearchEntityRegistry(
[
    SearchEntity.RelationalRaw("product"),
    SearchEntity.Document("order")
]));
builder.Services.AddSingleton<ISearchFieldDefinitionProvider>(_ => SimulatedFieldDefinitionDatabase.Create());
builder.Services.AddSingleton<SearchFieldDefinitionResolver>();
builder.Services.AddSingleton<DbBackedSearchMapProvider>();

// --- Layer di ricerca: un handler per entità/store, dietro un facade unico (ISearchService) ---
// product = SQL grezzo (PostgresRaw). Aggiungere un'entità = registrare un altro ISearchHandler.
builder.Services.AddSingleton<SqlSearchExecutor>(); // riceve ILogger<SqlSearchExecutor> → logga SQL + parametri
builder.Services.AddSingleton<ISearchHandler>(sp => new SqlSearchHandler(
    "product",
    sp.GetRequiredService<DbBackedSearchMapProvider>(),
    sp.GetRequiredService<ISqlSchemaProvider>(),
    sp.GetRequiredService<ICatalogConnectionFactory>(),
    sp.GetRequiredService<SqlSearchExecutor>()));
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
    catch (DomainException ex)
    {
        await WriteProblem(context, StatusCodes.Status422UnprocessableEntity, ex.Message);
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

// Schema (spike: EnsureCreated; in produzione → migrazioni) e seed se il DB è vuoto. In uno scope perché
// il repository/DbContext sono scoped.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.EnsureCreated();
    var repo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
}

app.Run();

// === Helper: eccezioni → problema JSON, e seed iniziale ===

static Task WriteProblem(HttpContext context, int status, string detail)
{
    context.Response.StatusCode = status;
    return context.Response.WriteAsJsonAsync(new { status, detail });
}