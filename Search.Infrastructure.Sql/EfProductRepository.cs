using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Search.Application.Catalog;
using Search.Domain.Catalog.Products;
using Search.Infrastructure.Sql.EF;

namespace Search.Infrastructure.Sql;

/// <summary>Repository prodotti su EF Core. <see cref="Query"/> è l'<c>IQueryable</c> tradotto in SQL.</summary>
public sealed class EfProductRepository : IProductRepository
{
    private readonly CatalogDbContext _db;

    public EfProductRepository(CatalogDbContext db) => _db = db;

    public void Add(Product product)
    {
        _db.Products.Add(product);
        _db.SaveChanges();
    }

    public Product? Get(Guid id) => _db.Products.FirstOrDefault(p => p.Id == id);

    public IReadOnlyList<Product> List() => _db.Products.ToList();

    public IQueryable<Product> Query() => _db.Products;

    /// <summary>Restituisce l'SQL che EF genererebbe per il predicato, senza eseguirlo (per ispezione).</summary>
    public string ToSql(Expression<Func<Product, bool>> predicate) => _db.Products.Where(predicate).ToQueryString();
}
