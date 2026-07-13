using MongoDB.Bson;
using Search.Application.Maps;
using Search.Application.Querying;
using Search.Application.Querying.Filters;
using Search.Application.Querying.Linq;
using Search.Application.Querying.Validation;
using Search.Infrastructure.Mongo;
using Search.Domain.Catalog.Brands;
using Search.Domain.Catalog.Products;
using Search.Domain.Catalog.Products.ValueObjects;
using Search.Domain.Common;
using Search.Domain.Common.ValueObjects;
using Search.Domain.Ordering.Orders;
using Search.Domain.Ordering.Orders.ValueObjects;

// Demo che esercita i modelli di dominio (STEP 1) end-to-end.
// Simuliamo qui, a mano, quello che in produzione farebbe l'infrastruttura:
//   - un IClock che dà "l'adesso"
//   - un IUserContext che dà l'utente corrente
//   - un audit interceptor che stampa Created/Modified al salvataggio.

/*
var now = DateTimeOffset.UtcNow;
const string currentUser = "lorenzo.maestrini@we-byte.it";

// Finto "interceptor": marca la creazione e stampa lo stato di audit.
static void SaveNew(AggregateRoot<Guid> aggregate, string user, DateTimeOffset at)
{
    aggregate.ApplyCreationAudit(user, at);
    Console.WriteLine($"  [audit] created by {aggregate.CreatedBy} at {aggregate.CreatedAt:u}");
    foreach (var evt in aggregate.DomainEvents)
        Console.WriteLine($"  [event] {evt.GetType().Name}");
    aggregate.ClearDomainEvents();
}

Console.WriteLine("== CATALOG (PostgreSQL) ==");

var brand = Brand.Create(
    name: "ACME Corp",
    code: "ACME",
    countryOfOrigin: "it",
    website: new Uri("https://acme.example"));
Console.WriteLine($"Brand: {brand.Name} [{brand.Code}] active={brand.IsActive}");
SaveNew(brand, currentUser, now);

var product = Product.Create(
    sku: Sku.Create("acme-widget-01"),
    name: "Widget Pro",
    brandId: brand.Id,
    price: Money.Of(19.90m, "EUR"),
    category: "widgets");
product.AddStock(100);
product.AddTag("novità");
product.AddTag("Novità"); // duplicato: ignorato dopo la normalizzazione
product.AddBarcode("8001234567890");
product.Publish();
product.ChangePrice(Money.Of(17.50m, "EUR"));
Console.WriteLine($"Product: {product.Name} sku={product.Sku} price={product.Price} " +
                  $"status={product.Status} stock={product.StockQuantity} tags=[{string.Join(", ", product.Tags)}]");
SaveNew(product, currentUser, now);

Console.WriteLine();
Console.WriteLine("== ORDERING (MongoDB) ==");

var order = Order.Create(
    orderNumber: "ORD-2026-00042",
    customer: CustomerInfo.Create(Guid.NewGuid(), "Mario Rossi", "mario.rossi@example.com"),
    shippingAddress: Address.Create("Via Roma 1", "Milano", "20100", "it"),
    currency: "EUR");
order.AddLine(product.Id, product.Name, product.Sku.Value, product.Price, quantity: 2);
order.AddLine(product.Id, product.Name, product.Sku.Value, product.Price, quantity: 1); // accorpata: qty=3
order.AddTag("priority");
order.Place(now);
order.Confirm();
order.MarkAsPaid(now.AddMinutes(5));
Console.WriteLine($"Order: {order.OrderNumber} status={order.Status} " +
                  $"lines={order.Lines.Count} total={order.TotalAmount}");
SaveNew(order, currentUser, now);

// Dimostra soft delete + audit di modifica.
product.MarkAsDeleted(currentUser, now.AddHours(1));
product.ApplyModificationAudit(currentUser, now.AddHours(1));
Console.WriteLine();
Console.WriteLine($"Product soft-deleted: isDeleted={product.IsDeleted} " +
                  $"deletedBy={product.DeletedBy} lastModifiedAt={product.LastModifiedAt:u}");

// Dimostra la protezione delle invarianti.
try
{
    order.AddLine(product.Id, product.Name, product.Sku.Value, product.Price, 1);
}
catch (DomainException ex)
{
    Console.WriteLine($"Invariante protetta: {ex.Message}");
}
*/

Console.WriteLine();
Console.WriteLine("== SEARCH ENGINE (opzione 1) ==");

// Dataset in memoria (in produzione sarebbe un IQueryable<Product> di EF Core su Postgres).
var catalogBrandId = Guid.NewGuid();

Product BuildProduct(string sku, string productName, decimal price, ProductStatus status, params string[] tags)
{
    var p = Product.Create(Sku.Create(sku), productName, catalogBrandId, Money.Of(price, "EUR"));
    foreach (var t in tags)
        p.AddTag(t);
    p.AddStock(10);
    p.Publish(); // -> Active
    if (status == ProductStatus.OutOfStock)
        p.RemoveStock(10); // -> OutOfStock
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

var productMap = new ProductSearchMap();
var validator = new SearchRequestValidator(productMap);
var executor = new LinqSearchExecutor<Product>(productMap);

// Filtro complesso: price >= 10 AND ( tags ⊇ {sale|novità} OR NOT(status == Discontinued) )
var request = new SearchRequest
{
    Filter = Filter.And(
        Filter.Gte("price", 10),
        Filter.Or(
            Filter.ArrayContainsAny("tags", "sale", "novità"),
            Filter.Not(Filter.Eq("status", "Discontinued"))
        )
    ),
    Projection = ["name", "price", "status", "tags"],
    Sort = [new SortField("price", SortDirection.Descending)],
    Page = new PageRequest(1, 10)
};

validator.Validate(request);
var result = executor.Execute(products.AsQueryable(), request);

Console.WriteLine($"Match: {result.TotalCount} — pagina {result.PageNumber}/{result.TotalPages}");
foreach (var row in result.Items)
    Console.WriteLine("  " + string.Join(", ", row.Select(kv => $"{kv.Key}={FormatValue(kv.Value)}")));

// La validazione blocca operatori incoerenti col tipo: Contains su un campo numerico.
try
{
    validator.Validate(new SearchRequest { Filter = Filter.Contains("price", "10") });
}
catch (SearchValidationException ex)
{
    Console.WriteLine("Validazione ha bloccato: " + string.Join("; ", ex.Errors));
}

Console.WriteLine();
Console.WriteLine("== SEARCH SU MONGO (stesso contratto, altro store) ==");

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
    BuildOrder("ORD-1", "mario.rossi@example.com", 17.50m, 3, OrderStatus.Paid, "priority"),  // total 52.50
    BuildOrder("ORD-2", "lucia@test.it", 10.00m, 2, OrderStatus.Draft),                       // total 20.00
    BuildOrder("ORD-3", "gino@test.it", 40.00m, 1, OrderStatus.Shipped),                      // total 40.00
    BuildOrder("ORD-4", "anna@example.com", 60.00m, 2, OrderStatus.Paid),                     // total 120.00
};

var orderMap = new OrderSearchMap();
var orderValidator = new SearchRequestValidator(orderMap);

// status ∈ {Paid, Shipped} AND total >= 50 AND ( email ~ "example.com" OR tags ⊇ {priority} )
var orderFilter = Filter.And(
    Filter.In("status", "Paid", "Shipped"),
    Filter.Gte("total", 50m),
    Filter.Or(
        Filter.Contains("customerEmail", "example.com"),
        Filter.ArrayContainsAny("tags", "priority")
    )
);

var orderRequest = new SearchRequest
{
    Filter = orderFilter,
    Projection = ["orderNumber", "status", "total", "customerEmail"]
};
orderValidator.Validate(orderRequest);

// (1) Semantica: lo stesso albero eseguito in memoria con il translator LINQ.
var orderExecutor = new LinqSearchExecutor<Order>(orderMap);
var orderResult = orderExecutor.Execute(orders.AsQueryable(), orderRequest);
Console.WriteLine($"Match (LINQ in memoria): {orderResult.TotalCount}");
foreach (var row in orderResult.Items)
    Console.WriteLine("  " + string.Join(", ", row.Select(kv => $"{kv.Key}={FormatValue(kv.Value)}")));

// (2) Stesso albero -> query MongoDB.
var mongoTranslator = new MongoFilterTranslator<Order>(orderMap);
var mongoQuery = mongoTranslator.BuildFilterDocument(orderFilter);
Console.WriteLine("Query Mongo generata dallo stesso filtro:");
Console.WriteLine("  " + mongoQuery.ToJson());

static string FormatValue(object? value) => value switch
{
    null => "null",
    System.Collections.IEnumerable enumerable and not string => "[" + string.Join("|", enumerable.Cast<object?>()) + "]",
    _ => value.ToString()!
};
