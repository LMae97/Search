namespace Search.Domain.Catalog.Products;

/// <summary>Ciclo di vita di un prodotto a catalogo.</summary>
public enum ProductStatus
{
    /// <summary>Bozza: non ancora pubblicato, non acquistabile.</summary>
    Draft = 0,

    /// <summary>Attivo e disponibile.</summary>
    Active = 1,

    /// <summary>Pubblicato ma senza giacenza.</summary>
    OutOfStock = 2,

    /// <summary>Fuori produzione: non ripristinabile.</summary>
    Discontinued = 3
}
