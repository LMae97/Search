using Search.Domain.Catalog.Products;

namespace Search.Application.Catalog;

/// <summary>
/// Repository dei prodotti. Per ora esiste solo un'implementazione in memoria; la firma è pensata
/// per essere soddisfatta domani da EF Core (<see cref="Query"/> diventa un <c>DbSet</c>/<c>IQueryable</c>
/// tradotto in SQL). Le mutazioni avvengono sull'aggregato caricato con <see cref="Get"/>.
/// </summary>
public interface IProductRepository
{
    void Add(Product product);

    Product? Get(Guid id);

    IReadOnlyList<Product> List();

    /// <summary>Sorgente interrogabile per il motore di ricerca (LINQ in memoria oggi, EF domani).</summary>
    IQueryable<Product> Query();
}
