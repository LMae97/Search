using Search.Domain.Catalog.Products.ValueObjects;
using Search.Domain.Catalog.Tags;
using Search.Domain.Common;
using Search.Domain.Common.ValueObjects;

namespace Search.Domain.Catalog.Products;

/// <summary>
/// Prodotto a catalogo. Aggregato radice persistito su PostgreSQL.
/// Referenzia la marca <b>per Id</b> (<see cref="BrandId"/>) e non con una navigation property:
/// Brand e Product sono aggregati distinti, con confini transazionali separati.
/// </summary>
public sealed class Product : AggregateRoot<Guid>
{
    // Backing field per le collezioni: incapsulamento reale, niente Add/Remove dall'esterno.
    private readonly List<Tag> _tags = [];
    private readonly List<string> _barcodes = [];

    public Sku Sku { get; private set; } = default!;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid BrandId { get; private set; }
    public Money Price { get; private set; } = default!;
    public int StockQuantity { get; private set; }
    public ProductStatus Status { get; private set; }
    public string? Category { get; private set; }
    public Dimensions? Dimensions { get; private set; }
    public decimal? WeightInGrams { get; private set; }

    /// <summary>
    /// Tag associati: relazione <b>molti-a-molti</b> con <see cref="Tag"/> (EF la gestisce come
    /// skip navigation sulla tabella di giunzione). Nella ricerca è proiettata sui nomi/id dei tag.
    /// </summary>
    public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();

    /// <summary>Codici a barre (EAN/UPC). Anch'esso un campo array.</summary>
    public IReadOnlyCollection<string> Barcodes => _barcodes.AsReadOnly();

    private Product()
    {
        // Riservato all'ORM.
    }

    private Product(Guid id, Sku sku, string name, Guid brandId, Money price) : base(id)
    {
        Sku = sku;
        Name = name;
        BrandId = brandId;
        Price = price;
        Status = ProductStatus.Draft;
        StockQuantity = 0;
    }

    public static Product Create(
        Sku sku,
        string name,
        Guid brandId,
        Money price,
        string? description = null,
        string? category = null)
    {
        ArgumentNullException.ThrowIfNull(sku);
        ArgumentNullException.ThrowIfNull(price);
        DomainGuard.Against(brandId == Guid.Empty, "Un prodotto deve appartenere a una marca.");

        return new Product(Guid.NewGuid(), sku, NormalizeName(name), brandId, price)
        {
            Description = description?.Trim(),
            Category = category?.Trim()
        };
    }

    public void Rename(string newName) => Name = NormalizeName(newName);

    public void UpdateDetails(string? description, string? category, Dimensions? dimensions, decimal? weightInGrams)
    {
        DomainGuard.Against(weightInGrams is < 0, "Il peso non può essere negativo.");
        Description = description?.Trim();
        Category = category?.Trim();
        Dimensions = dimensions;
        WeightInGrams = weightInGrams;
    }

    public void ChangePrice(Money newPrice)
    {
        ArgumentNullException.ThrowIfNull(newPrice);
        if (newPrice == Price)
            return;
        var previous = Price;
        Price = newPrice;
        RaiseDomainEvent(new ProductPriceChangedDomainEvent(Id, previous.Amount, newPrice.Amount, newPrice.Currency));
    }

    public void AddStock(int quantity)
    {
        DomainGuard.Against(quantity <= 0, "La quantità di rifornimento deve essere positiva.");
        StockQuantity += quantity;
        if (Status == ProductStatus.OutOfStock)
            Status = ProductStatus.Active;
    }

    public void RemoveStock(int quantity)
    {
        DomainGuard.Against(quantity <= 0, "La quantità da scaricare deve essere positiva.");
        DomainGuard.Against(quantity > StockQuantity, "Giacenza insufficiente.");
        StockQuantity -= quantity;
        if (StockQuantity == 0 && Status == ProductStatus.Active)
            Status = ProductStatus.OutOfStock;
    }

    public void Publish()
    {
        DomainGuard.Against(Status == ProductStatus.Discontinued, "Un prodotto fuori produzione non può essere pubblicato.");
        Status = StockQuantity > 0 ? ProductStatus.Active : ProductStatus.OutOfStock;
    }

    public void Discontinue()
    {
        if (Status == ProductStatus.Discontinued)
            return;
        Status = ProductStatus.Discontinued;
        RaiseDomainEvent(new ProductDiscontinuedDomainEvent(Id));
    }

    public void AddTag(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        // Dedup per identità dell'entità (Entity.Equals confronta l'Id): lo stesso tag non si ripete.
        if (!_tags.Contains(tag))
            _tags.Add(tag);
    }

    public void RemoveTag(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        _tags.Remove(tag);
    }

    public void AddBarcode(string barcode)
    {
        DomainGuard.Against(string.IsNullOrWhiteSpace(barcode), "Il codice a barre non può essere vuoto.");
        var normalized = barcode.Trim();
        if (!_barcodes.Contains(normalized))
            _barcodes.Add(normalized);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Il nome del prodotto è obbligatorio.");
        return name.Trim();
    }
}
