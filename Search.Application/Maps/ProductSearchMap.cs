using Search.Application.Querying.Metadata;
using Search.Domain.Catalog.Products;

namespace Search.Application.Maps;

/// <summary>
/// Mappa di ricerca del prodotto: dichiara quali campi sono ricercabili/proiettabili.
/// Solo i campi elencati qui sono esposti al FE — è la whitelist. La categoria di tipo e
/// gli operatori ammessi sono inferiti dal selettore; per casi speciali si possono passare
/// esplicitamente gli operatori.
/// </summary>
public sealed class ProductSearchMap : EntitySearchMap<Product>
{
    public override string EntityName => "product";

    public ProductSearchMap()
    {
        // Identità e anagrafica
        MapField("id", p => p.Id);
        MapField("name", p => p.Name);
        MapField("description", p => p.Description);
        MapField("sku", p => p.Sku.Value);
        MapField("brandId", p => p.BrandId);
        MapField("category", p => p.Category);

        // Prezzo (value object -> due campi proiettabili distinti)
        MapField("price", p => p.Price.Amount);
        MapField("currency", p => p.Price.Currency);

        // Numerici / stato
        MapField("stock", p => p.StockQuantity);
        MapField("status", p => p.Status);
        MapField("weightInGrams", p => p.WeightInGrams);

        // Array
        MapArray("tags", p => p.Tags);
        MapArray("barcodes", p => p.Barcodes);

        // Colonne di audit / soft delete (ricercabili e proiettabili)
        MapField("createdAt", p => p.CreatedAt);
        MapField("createdBy", p => p.CreatedBy);
        MapField("lastModifiedAt", p => p.LastModifiedAt);
        MapField("isDeleted", p => p.IsDeleted);
    }
}
