using System.Text.Json.Serialization;
using Search.Api.Repositories;
using Search.Api.Serialization;
using Search.Application.Catalog;
using Search.Application.Querying.Dynamic;
using Search.Domain.Catalog.Products;
using Search.Domain.Catalog.Products.ValueObjects;
using Search.Domain.Catalog.Tags;
using Search.Domain.Common;
using Search.Domain.Common.ValueObjects;
using Search.Application.Querying.Validation;

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

// Composizione: repository + motore di ricerca (definizioni "a DB").
builder.Services.AddSingleton<IProductRepository, InMemoryProductRepository>();
builder.Services.AddSingleton(new SearchEntityRegistry(
[
    SearchEntity.Relational<Product>("product"),
    SearchEntity.Document("order")
]));
builder.Services.AddSingleton<ISearchFieldDefinitionProvider, SimulatedFieldDefinitionDatabase>();
builder.Services.AddSingleton<SearchFieldDefinitionResolver>();
builder.Services.AddSingleton<DbBackedSearchMapProvider>();

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

SeedProducts(app.Services.GetRequiredService<IProductRepository>());

app.Run();

// === Helper: eccezioni → problema JSON, e seed iniziale ===

static Task WriteProblem(HttpContext context, int status, string detail)
{
    context.Response.StatusCode = status;
    return context.Response.WriteAsJsonAsync(new { status, detail });
}

static void SeedProducts(IProductRepository repo)
{
    var brandId = Guid.NewGuid();

    Product Make(string sku, string name, decimal price, ProductStatus status, params string[] tags)
    {
        var product = Product.Create(Sku.Create(sku), name, brandId, Money.Of(price, "EUR"));
        foreach (var tag in tags)
            product.AddTag(Tag.Create(tag));
        product.AddStock(10);
        product.Publish();
        if (status == ProductStatus.OutOfStock)
            product.RemoveStock(10);
        else if (status == ProductStatus.Discontinued)
            product.Discontinue();
        product.ApplyCreationAudit("seed@we-byte.it", DateTimeOffset.UtcNow);
        repo.Add(product);
        return product;
    }

    Make("WIDGET-PRO", "Widget Pro", 17.50m, ProductStatus.Active, "novità");
    Make("GADGET", "Gadget", 5.00m, ProductStatus.Active, "sale");
    Make("BUNDLE", "Bundle", 49.90m, ProductStatus.OutOfStock, "sale", "novità");
}
