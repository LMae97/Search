using Search.Domain.Catalog.Products;

namespace Search.Application.Catalog;

/// <summary>
/// Repository dei prodotti. Per ora esiste solo un'implementazione in memoria; la firma è pensata
/// per essere soddisfatta domani da EF Core (<see cref="Query"/> diventa un <c>DbSet</c>/<c>IQueryable</c>
/// tradotto in SQL). Le mutazioni avvengono sull'aggregato caricato con <see cref="Get"/>.
/// <para>
/// <b>Nota di layering (rif. revisione 2026-07-14):</b> questo è un contratto CRUD di persistenza del catalogo,
/// <b>non</b> parte del motore di ricerca store-agnostic (gli executor prendono direttamente
/// <c>IQueryable&lt;T&gt;</c>/<c>IMongoCollection</c>, non questo tipo). Vive qui per pragmatismo (è l'unico
/// assembly comune ai consumer). Se un domani il motore diventasse una libreria a sé, andrebbe estratto in un
/// layer applicativo di catalogo dedicato — <b>non</b> in Search.Api (romperebbe il layering: Infrastructure.Sql
/// che implementa l'interfaccia finirebbe per dipendere dall'host web).
/// </para>
/// </summary>
public interface IProductRepository
{
    void Add(Product product);

    Product? Get(Guid id);

    IReadOnlyList<Product> List();

    /// <summary>Sorgente interrogabile per il motore di ricerca (LINQ in memoria oggi, EF domani).</summary>
    IQueryable<Product> Query();
}
