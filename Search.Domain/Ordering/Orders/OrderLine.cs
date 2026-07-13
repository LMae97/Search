using Search.Domain.Common;
using Search.Domain.Common.ValueObjects;

namespace Search.Domain.Ordering.Orders;

/// <summary>
/// Riga d'ordine. Entità <b>interna</b> all'aggregato <see cref="Order"/>: si crea e si modifica
/// solo passando dalla radice (costruttore e metodi <c>internal</c>).
/// I dati di prodotto (nome, sku, prezzo) sono uno <b>snapshot</b> al momento dell'acquisto:
/// non importiamo i value object del contesto Catalog, per non accoppiare i due domini.
/// </summary>
public sealed class OrderLine : Entity<Guid>
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>SKU come stringa: snapshot denormalizzato, non il VO <c>Sku</c> del Catalog.</summary>
    public string Sku { get; private set; } = string.Empty;

    public Money UnitPrice { get; private set; } = default!;
    public int Quantity { get; private set; }

    /// <summary>Totale di riga = prezzo unitario × quantità. Proprietà derivata.</summary>
    public Money LineTotal => UnitPrice.MultiplyBy(Quantity);

    private OrderLine()
    {
        // Riservato al serializzatore Mongo.
    }

    internal OrderLine(Guid productId, string productName, string sku, Money unitPrice, int quantity)
        : base(Guid.NewGuid())
    {
        DomainGuard.Against(productId == Guid.Empty, "Il ProductId della riga è obbligatorio.");
        DomainGuard.Against(string.IsNullOrWhiteSpace(productName), "Il nome prodotto è obbligatorio.");
        DomainGuard.Against(string.IsNullOrWhiteSpace(sku), "Lo SKU è obbligatorio.");
        ArgumentNullException.ThrowIfNull(unitPrice);
        DomainGuard.Against(quantity <= 0, "La quantità deve essere positiva.");

        ProductId = productId;
        ProductName = productName.Trim();
        Sku = sku.Trim().ToUpperInvariant();
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    internal void ChangeQuantity(int newQuantity)
    {
        DomainGuard.Against(newQuantity <= 0, "La quantità deve essere positiva.");
        Quantity = newQuantity;
    }

    internal void Increase(int by)
    {
        DomainGuard.Against(by <= 0, "L'incremento deve essere positivo.");
        Quantity += by;
    }
}
