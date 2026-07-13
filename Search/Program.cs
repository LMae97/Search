using MongoDB.Bson;
using Search.Application.Querying;
using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Filters;
using Search.Application.Querying.Linq;
using Search.Application.Querying.Validation;
using Search.Domain.Catalog.Products;
using Search.Domain.Catalog.Products.ValueObjects;
using Search.Domain.Catalog.Tags;
using Search.Domain.Common.ValueObjects;
using Search.Domain.Ordering.Orders;
using Search.Domain.Ordering.Orders.ValueObjects;
using Search.Infrastructure.Mongo;
using Search.Simulation;

// ===========================================================================================
// Dati in memoria. In produzione: IQueryable<Product> (EF Core / Postgres) e
// IMongoCollection<Order> (Mongo). Qui liste, così il motore gira senza infrastruttura reale.
// ===========================================================================================

var catalogBrandId = Guid.NewGuid();

// Catalogo tag condiviso: lo stesso Tag è riusato da più prodotti (relazione molti-a-molti).
var tagCatalog = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);
Tag TagByName(string name) => tagCatalog.TryGetValue(name, out var existing) ? existing : tagCatalog[name] = Tag.Create(name);

Product BuildProduct(string sku, string productName, decimal price, ProductStatus status, params string[] tags)
{
    var p = Product.Create(Sku.Create(sku), productName, catalogBrandId, Money.Of(price, "EUR"));
    foreach (var t in tags)
        p.AddTag(TagByName(t));
    p.AddStock(10);
    p.Publish();
    if (status == ProductStatus.OutOfStock)
        p.RemoveStock(10);
    else if (status == ProductStatus.Discontinued)
        p.Discontinue();
    return p;
}

var products = new List<Product>
{
    BuildProduct("WIDGET-PRO", "Widget Pro", 17.50m, ProductStatus.Active, "novità"),
    BuildProduct("GADGET", "Gadget", 5.00m, ProductStatus.Active, "sale"),
    BuildProduct("OLD-THING", "Old Thing", 99.00m, ProductStatus.Discontinued),
    BuildProduct("BUNDLE", "Bundle", 49.90m, ProductStatus.OutOfStock, "sale", "novità"),
};

Order BuildOrder(string number, string email, decimal unitPrice, int quantity, OrderStatus target, params string[] orderTags)
{
    var o = Order.Create(
        number,
        CustomerInfo.Create(Guid.NewGuid(), "Cliente " + number, email),
        Address.Create("Via Milano 1", "Milano", "20100", "it"));
    o.AddLine(Guid.NewGuid(), "Prodotto", "SKU-" + number, Money.Of(unitPrice, "EUR"), quantity);
    foreach (var t in orderTags)
        o.AddTag(t);

    var ts = DateTimeOffset.UtcNow;
    if (target is not OrderStatus.Draft) o.Place(ts);
    if (target is OrderStatus.Confirmed or OrderStatus.Paid or OrderStatus.Shipped or OrderStatus.Delivered) o.Confirm();
    if (target is OrderStatus.Paid or OrderStatus.Shipped or OrderStatus.Delivered) o.MarkAsPaid(ts);
    if (target is OrderStatus.Shipped or OrderStatus.Delivered) o.Ship(ts);
    if (target is OrderStatus.Delivered) o.Deliver(ts);
    return o;
}

var orders = new List<Order>
{
    BuildOrder("ORD-1", "mario.rossi@example.com", 17.50m, 3, OrderStatus.Paid, "priority"), // total 52.50
    BuildOrder("ORD-2", "lucia@test.it", 10.00m, 2, OrderStatus.Draft),                       // total 20.00
    BuildOrder("ORD-3", "gino@test.it", 40.00m, 1, OrderStatus.Shipped),                      // total 40.00
    BuildOrder("ORD-4", "anna@example.com", 60.00m, 2, OrderStatus.Paid),                     // total 120.00
};

// ===========================================================================================
// Motore di ricerca, guidato dalle definizioni dei campi "a DB" (SimulatedFieldDefinitionDatabase).
//   definizioni -> resolver (path→binding) -> mappa effettiva (per tenant + permessi) -> pipeline
// ===========================================================================================

var fieldDatabase = new SimulatedFieldDefinitionDatabase();
var spaceA = SimulatedFieldDefinitionDatabase.DemoSpace;
var searchEntities = new SearchEntityRegistry(new[]
{
    SearchEntity.Relational<Product>("product"), // relazionale: il path è una property-path CLR
    SearchEntity.Document("order")               // documentale: il path è il path del documento Mongo
});
var dbMaps = new DbBackedSearchMapProvider(fieldDatabase, new SearchFieldDefinitionResolver(searchEntities));

// --- Prodotto (store relazionale, eseguito in memoria) ---
// "tags" è definito a DB solo come path "Tags.Name" (collezione M2M) → ricostruito in x.Tags.Select(t => t.Name).
// "price" richiede il permesso ViewPrice → chi non ce l'ha se lo vede rimosso da filtro/proiezione/sort.
Console.WriteLine("== PRODOTTO (relazionale, da definizioni DB) ==");

var productRequest = new SearchRequest
{
    Filter = Filter.And(Filter.Gte("price", 10), Filter.ArrayContainsAny("tags", "sale", "novità")),
    Projection = ["name", "price", "tags"],
    Sort = [new SortField("price", SortDirection.Descending)]
};

void RunProduct(string who, SearchCaller caller)
{
    var map = dbMaps.GetEffectiveMap("product", caller);
    var sanitized = new SearchRequestSanitizer(map).Sanitize(productRequest);
    new SearchRequestValidator(map).Validate(sanitized);
    var result = new LinqSearchExecutor<Product>(map).Execute(products.AsQueryable(), sanitized);

    var columns = result.Items.Count > 0 ? string.Join(", ", result.Items[0].Keys) : "(nessuna colonna)";
    Console.WriteLine($"[{who}] colonne: {columns} | match: {result.TotalCount}");
    foreach (var row in result.Items)
        Console.WriteLine("   " + string.Join(", ", row.Select(kv => $"{kv.Key}={FormatValue(kv.Value)}")));
}

RunProduct("Manager (ha ViewPrice)", new SearchCaller(spaceA, new HashSet<Guid> { SearchPermissions.ViewPrice }));
RunProduct("Clerk (NON ha ViewPrice)", new SearchCaller(spaceA, new HashSet<Guid>()));

// --- Ordine (store documentale, tradotto in query Mongo) ---
// "deliveryZone" è un campo dinamico definito solo per il tenant spaceA.
Console.WriteLine();
Console.WriteLine("== ORDINE (documentale, da definizioni DB) ==");

var orderFilter = Filter.And(Filter.In("status", "Paid", "Shipped"), Filter.Eq("deliveryZone", "Nord"));

void RunOrderMongo(string who, SearchCaller caller)
{
    var map = dbMaps.GetEffectiveMap("order", caller);
    var sanitized = new SearchRequestSanitizer(map).Sanitize(new SearchRequest { Filter = orderFilter });
    var query = new MongoFilterTranslator<Order>(map).BuildFilterDocument(sanitized.Filter!);
    Console.WriteLine($"[{who}] query Mongo: {query.ToJson()}");
}

RunOrderMongo("Tenant con deliveryZone", new SearchCaller(spaceA, new HashSet<Guid>()));
RunOrderMongo("Altro tenant (senza)", new SearchCaller(Guid.NewGuid(), new HashSet<Guid>()));

static string FormatValue(object? value) => value switch
{
    null => "null",
    System.Collections.IEnumerable enumerable and not string => "[" + string.Join("|", enumerable.Cast<object?>()) + "]",
    _ => value.ToString()!
};
