using Microsoft.EntityFrameworkCore;
using Search.Domain.Catalog.Products;
using Search.Domain.Catalog.Products.ValueObjects;
using Search.Domain.Catalog.Tags;

namespace Search.Infrastructure.Sql;

/// <summary>
/// DbContext del catalogo (Postgres in produzione, SQLite per lo spike). Mostra come si mappa
/// il dominio ricco: value object come owned/converted, M2M come skip navigation, enum come stringa,
/// soft-delete come filtro globale. Gli stessi selettori del motore di ricerca diventano SQL qui.
/// </summary>
public sealed class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var product = modelBuilder.Entity<Product>();
        product.ToTable("Products");
        product.HasKey(p => p.Id);

        // Sku (value object) → colonna string via converter.
        product.Property(p => p.Sku)
            .HasConversion(sku => sku.Value, value => Sku.Create(value))
            .HasColumnName("Sku");

        product.Property(p => p.Name);
        product.Property(p => p.Description);
        product.Property(p => p.BrandId);
        product.Property(p => p.StockQuantity);
        product.Property(p => p.Category);
        product.Property(p => p.WeightInGrams);

        // Enum come stringa (leggibile, coerente con la convenzione Mongo).
        product.Property(p => p.Status).HasConversion<string>();

        // Money → owned (due colonne).
        product.OwnsOne(p => p.Price, price =>
        {
            price.Property(m => m.Amount).HasColumnName("PriceAmount");
            price.Property(m => m.Currency).HasColumnName("PriceCurrency");
        });

        // Dimensions → owned opzionale (EF crea 3 colonne nullable). La proprietà calcolata VolumeMm3 non viene mappata.
        product.OwnsOne(p => p.Dimensions, dimensions =>
        {
            dimensions.Property(d => d.LengthMm).HasColumnName("DimLengthMm");
            dimensions.Property(d => d.WidthMm).HasColumnName("DimWidthMm");
            dimensions.Property(d => d.HeightMm).HasColumnName("DimHeightMm");
            dimensions.Ignore(d => d.VolumeMm3); // proprietà calcolata
        });

        product.Ignore(p => p.Barcodes);      // non persistiti
        product.Ignore(p => p.DomainEvents);  // non persistiti

        // Molti-a-molti con Tag (skip navigation): EF crea la tabella di giunzione.
        product.HasMany(p => p.Tags).WithMany();

        // Soft delete → filtro globale.
        product.HasQueryFilter(p => !p.IsDeleted);

        var tag = modelBuilder.Entity<Tag>();
        tag.ToTable("Tags");
        tag.HasKey(t => t.Id);
        tag.Property(t => t.Name);
        tag.Property(t => t.Slug);
        tag.Ignore(t => t.DomainEvents);
        tag.HasQueryFilter(t => !t.IsDeleted);
    }
}
