using EphemeralMongo;
using Microsoft.EntityFrameworkCore;
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
using Search.Infrastructure.Sql;

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

// --- Case-insensitivity del contains (coerente con Mongo) ---
Console.WriteLine();
Console.WriteLine("== CASE-INSENSITIVITY (contains) ==");
var ciMap = dbMaps.GetEffectiveMap("product", new SearchCaller(spaceA, new HashSet<Guid>()));
foreach (var term in new[] { "widget", "WIDGET", "WiDgEt" })
{
    var request = new SearchRequest { Filter = Filter.Contains("name", term), Projection = ["name"] };
    var sanitized = new SearchRequestSanitizer(ciMap).Sanitize(request);
    var result = new LinqSearchExecutor<Product>(ciMap).Execute(products.AsQueryable(), sanitized);
    var names = string.Join(", ", result.Items.Select(r => r["name"]));
    Console.WriteLine($"   name contains \"{term}\" → {result.TotalCount} match: {names}");
}

// --- Spike EF Core / SQLite: lo stesso motore su un DB reale → SQL generato ---
Console.WriteLine();
Console.WriteLine("== EF CORE / SQLITE: SQL generato dal motore ==");

using var db = SqliteCatalog.CreateInMemory();
var efRepo = new EfProductRepository(db);

var efBrandId = Guid.NewGuid();
void SeedEf(string sku, string name, decimal price, ProductStatus status, params string[] tags)
{
    var p = Product.Create(Sku.Create(sku), name, efBrandId, Money.Of(price, "EUR"));
    foreach (var t in tags)
        p.AddTag(Tag.Create(t));
    p.AddStock(10);
    p.Publish();
    if (status == ProductStatus.OutOfStock) p.RemoveStock(10);
    else if (status == ProductStatus.Discontinued) p.Discontinue();
    p.ApplyCreationAudit("seed@we-byte.it", DateTimeOffset.UtcNow);
    efRepo.Add(p);
}
SeedEf("WIDGET-PRO", "Widget Pro", 17.50m, ProductStatus.Active, "novità");
SeedEf("GADGET", "Gadget", 5.00m, ProductStatus.Active, "sale");
SeedEf("BUNDLE", "Bundle", 49.90m, ProductStatus.OutOfStock, "sale", "novità");

var efMap = dbMaps.GetEffectiveMap("product", new SearchCaller(spaceA, new HashSet<Guid> { SearchPermissions.ViewPrice }));

// (1) M2M: price >= 10 AND tags ⊇ {sale, novità} → subquery EXISTS sulla giunzione
var m2mFilter = Filter.And(Filter.Gte("price", 10), Filter.ArrayContainsAny("tags", "sale", "novità"));
var m2mPredicate = new LinqFilterTranslator<Product>(efMap).Translate(m2mFilter);
Console.WriteLine("SQL per  price>=10 AND tags ⊇ {sale,novità}:");
Console.WriteLine(efRepo.ToSql(m2mPredicate));

// (2) contains case-insensitive → LOWER(...) LIKE
var containsPredicate = new LinqFilterTranslator<Product>(efMap).Translate(Filter.Contains("name", "widget"));
Console.WriteLine();
Console.WriteLine("SQL per  name contains \"widget\" (case-insensitive):");
Console.WriteLine(efRepo.ToSql(containsPredicate));

// (3) esecuzione reale su SQLite tramite il motore
var efRequest = new SearchRequest
{
    Filter = m2mFilter,
    Projection = ["name", "price", "tags"],
    //Sort = [new SortField("price", SortDirection.Descending)]
};
Console.WriteLine();
Console.WriteLine();
Console.WriteLine();
Console.WriteLine();
var efSanitized = new SearchRequestSanitizer(efMap).Sanitize(efRequest);
var efResult = new LinqSearchExecutor<Product>(efMap).Execute(efRepo.Query(), efSanitized);
Console.WriteLine();
Console.WriteLine($"Eseguito su SQLite → {efResult.TotalCount} match:");
foreach (var row in efResult.Items)
    Console.WriteLine("   " + string.Join(", ", row.Select(kv => $"{kv.Key}={FormatValue(kv.Value)}")));

// (4) SINGLE QUERY vs SPLIT QUERY per la collezione proiettata (tags).
//     Stessa identica richiesta: cambia solo COME EF materializza la collezione.
//     - default (single): UN solo SELECT con JOIN → le colonne radice sono duplicate per ogni tag.
//     - .AsSplitQuery():   DUE SELECT (radici + tag correlati per chiave) → nessuna duplicazione.
//     Nota di design: .AsSplitQuery() è specifico di EF Core e il flag PROPAGA lungo la catena, quindi
//     lo applichiamo qui sull'IQueryable (adapter SQL) — l'executor in Search.Application resta puro e
//     gira anche in memoria. In produzione vive nel repo/DbContext, mai nel motore store-agnostic.
Console.WriteLine();
Console.WriteLine("== SINGLE QUERY (default): 1 SELECT con JOIN, righe radice duplicate per ogni tag ==");
new LinqSearchExecutor<Product>(efMap).Execute(efRepo.Query(), efSanitized);

Console.WriteLine();
Console.WriteLine("== SPLIT QUERY (.AsSplitQuery()): 2 SELECT separati, nessuna duplicazione ==");
new LinqSearchExecutor<Product>(efMap).Execute(efRepo.Query().AsSplitQuery(), efSanitized);

/*
// --- Mongo: la query completa generata dall'executor (filtro + proiezione + sort + paginazione) ---
Console.WriteLine();
Console.WriteLine("== MONGO: query generata dall'executor ==");
var mongoMap = dbMaps.GetEffectiveMap("order", new SearchCaller(spaceA, new HashSet<Guid>()));
var mongoRequest = new SearchRequest
{
    Filter = Filter.And(Filter.In("status", "Paid", "Shipped"), Filter.Eq("deliveryZone", "Nord")),
    Projection = ["status", "total", "customerEmail", "deliveryZone"],
    Sort = [new SortField("total", SortDirection.Descending)],
    Page = new PageRequest(1, 20)
};

var mongoPlan = new MongoSearchExecutor<BsonDocument>(mongoMap).BuildPlan(mongoRequest);
Console.WriteLine("filter:     " + mongoPlan.Filter.ToJson());
Console.WriteLine("projection: " + mongoPlan.Projection.ToJson());
Console.WriteLine("sort:       " + (mongoPlan.Sort?.ToJson() ?? "(nessuno)"));
Console.WriteLine($"skip/limit: {mongoPlan.Skip}/{mongoPlan.Limit}");

// --- Mongo: esecuzione reale su un mongod effimero (documenti schemaless con il bag "attributes") ---
Console.WriteLine();
Console.WriteLine("== MONGO: esecuzione reale (mongod effimero) ==");
try
{
    using var runner = MongoRunner.Run();
    var collection = new MongoDB.Driver.MongoClient(runner.ConnectionString)
        .GetDatabase("demo")
        .GetCollection<BsonDocument>("orders");

    collection.InsertMany(
    [
        BsonDocument.Parse("""{ "orderNumber": "ORD-1", "status": "Paid",    "totalAmount": { "amount": { "$numberDecimal": "52.50" } },  "customer": { "email": "mario@example.com" }, "attributes": { "deliveryZone": "Nord" } }"""),
        BsonDocument.Parse("""{ "orderNumber": "ORD-2", "status": "Draft",   "totalAmount": { "amount": { "$numberDecimal": "20.00" } },  "customer": { "email": "lucia@test.it" },     "attributes": { "deliveryZone": "Nord" } }"""),
        BsonDocument.Parse("""{ "orderNumber": "ORD-3", "status": "Shipped", "totalAmount": { "amount": { "$numberDecimal": "40.00" } },  "customer": { "email": "gino@test.it" },      "attributes": { "deliveryZone": "Sud"  } }"""),
        BsonDocument.Parse("""{ "orderNumber": "ORD-4", "status": "Paid",    "totalAmount": { "amount": { "$numberDecimal": "120.00" } }, "customer": { "email": "anna@example.com" },  "attributes": { "deliveryZone": "Nord" } }"""),
    ]);

    var mongoResult = new MongoSearchExecutor<BsonDocument>(mongoMap).Execute(collection, mongoRequest);
    Console.WriteLine($"Match: {mongoResult.TotalCount}");
    foreach (var row in mongoResult.Items)
        Console.WriteLine("   " + string.Join(", ", row.Select(kv => $"{kv.Key}={FormatValue(kv.Value)}")));
}
catch (Exception ex)
{
    Console.WriteLine("mongod effimero non disponibile in questo ambiente: " + ex.Message);
    Console.WriteLine("(l'executor resta pronto: Execute(collection, request) su un Mongo reale/Atlas.)");
}
*/

static string FormatValue(object? value) => value switch
{
    null => "null",
    System.Collections.IEnumerable enumerable and not string => "[" + string.Join("|", enumerable.Cast<object?>()) + "]",
    _ => value.ToString()!
};