using System.Collections.Concurrent;
using Search.Application.Catalog;
using Search.Domain.Catalog.Products;

namespace Search.Api.Repositories;

/// <summary>
/// Repository prodotti in memoria (thread-safe). Le mutazioni avvengono sull'istanza restituita da
/// <see cref="Get"/> (stesso riferimento), quindi non serve un metodo di "save" esplicito.
/// </summary>
public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<Guid, Product> _products = new();

    public void Add(Product product) => _products[product.Id] = product;

    public Product? Get(Guid id) => _products.TryGetValue(id, out var product) ? product : null;

    public IReadOnlyList<Product> List() => _products.Values.ToList();

    public IQueryable<Product> Query() => _products.Values.AsQueryable();
}
