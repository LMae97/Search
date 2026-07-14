using Search.Domain.Catalog.Products;
using Search.Domain.Catalog.Products.ValueObjects;

namespace Search.Api.Contracts;

/// <summary>Mapping dominio → DTO (e viceversa per i value object in ingresso).</summary>
public static class ProductMappings
{
    // list: solo id + code (sku) + label (name), come richiesto.
    public static ProductListItemDto ToListItem(Product product)
        => new(product.Id, product.Sku.Value, product.Name);

    // details: tutto, incluso stato e metadati di audit/soft-delete.
    public static ProductDetailsDto ToDetails(Product product) => new(
        product.Id,
        product.Sku.Value,
        product.Name,
        product.Description,
        product.BrandId,
        new MoneyDto(product.Price.Amount, product.Price.Currency),
        product.StockQuantity,
        product.Status.ToString(),
        product.Category,
        product.Dimensions is null
            ? null
            : new DimensionsDto(product.Dimensions.LengthMm, product.Dimensions.WidthMm, product.Dimensions.HeightMm),
        product.WeightInGrams,
        product.Tags.Select(tag => tag.Name).ToList(),
        product.Barcodes.ToList(),
        new AuditDto(
            product.CreatedAt, product.CreatedBy,
            product.LastModifiedAt, product.LastModifiedBy,
            product.IsDeleted, product.DeletedAt, product.DeletedBy));

    public static Dimensions? ToDomain(DimensionsDto? dimensions)
        => dimensions is null ? null : Dimensions.Create(dimensions.LengthMm, dimensions.WidthMm, dimensions.HeightMm);
}
