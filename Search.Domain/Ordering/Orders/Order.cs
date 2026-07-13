using Search.Domain.Common;
using Search.Domain.Common.ValueObjects;
using Search.Domain.Ordering.Orders.ValueObjects;

namespace Search.Domain.Ordering.Orders;

/// <summary>
/// Ordine. Aggregato radice persistito su MongoDB come singolo documento, con le righe
/// (<see cref="Lines"/>) e gli indirizzi embedded. La consistenza (totale, transizioni di stato,
/// modificabilità) è garantita solo attraverso i metodi della radice.
/// </summary>
public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = [];
    private readonly List<string> _tags = [];

    public string OrderNumber { get; private set; } = string.Empty;
    public CustomerInfo Customer { get; private set; } = default!;
    public OrderStatus Status { get; private set; }
    public Address ShippingAddress { get; private set; } = default!;
    public Address? BillingAddress { get; private set; }

    /// <summary>Valuta dell'ordine: tutte le righe devono usarla.</summary>
    public string Currency { get; private set; } = "EUR";

    /// <summary>Totale ordine, ricalcolato ad ogni modifica delle righe.</summary>
    public Money TotalAmount { get; private set; } = default!;

    public DateTimeOffset? PlacedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public DateTimeOffset? ShippedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    /// <summary>Righe d'ordine. Campo <b>array</b>: abilita filtri su elementi ("una riga con SKU X").</summary>
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();

    private Order()
    {
        // Riservato al serializzatore Mongo.
    }

    private Order(Guid id, string orderNumber, CustomerInfo customer, Address shippingAddress, string currency)
        : base(id)
    {
        OrderNumber = orderNumber;
        Customer = customer;
        ShippingAddress = shippingAddress;
        Currency = currency;
        Status = OrderStatus.Draft;
        TotalAmount = Money.Zero(currency);
    }

    public static Order Create(
        string orderNumber,
        CustomerInfo customer,
        Address shippingAddress,
        string currency = "EUR")
    {
        DomainGuard.Against(string.IsNullOrWhiteSpace(orderNumber), "Il numero d'ordine è obbligatorio.");
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(shippingAddress);
        DomainGuard.Against(
            string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3,
            "La valuta deve essere un codice ISO 4217.");

        return new Order(
            Guid.NewGuid(),
            orderNumber.Trim().ToUpperInvariant(),
            customer,
            shippingAddress,
            currency.Trim().ToUpperInvariant());
    }

    // --- Gestione righe (solo in bozza) ---

    public void AddLine(Guid productId, string productName, string sku, Money unitPrice, int quantity)
    {
        EnsureMutable();
        DomainGuard.Against(
            unitPrice.Currency != Currency,
            $"La valuta di riga {unitPrice.Currency} non coincide con quella dell'ordine {Currency}.");

        // Stesso prodotto allo stesso prezzo => si accorpano le quantità.
        var existing = _lines.FirstOrDefault(l => l.ProductId == productId && l.UnitPrice == unitPrice);
        if (existing is not null)
            existing.Increase(quantity);
        else
            _lines.Add(new OrderLine(productId, productName, sku, unitPrice, quantity));

        RecalculateTotal();
    }

    public void RemoveLine(Guid lineId)
    {
        EnsureMutable();
        var line = FindLine(lineId);
        _lines.Remove(line);
        RecalculateTotal();
    }

    public void ChangeLineQuantity(Guid lineId, int newQuantity)
    {
        EnsureMutable();
        FindLine(lineId).ChangeQuantity(newQuantity);
        RecalculateTotal();
    }

    public void SetBillingAddress(Address billingAddress)
    {
        ArgumentNullException.ThrowIfNull(billingAddress);
        BillingAddress = billingAddress;
    }

    // --- Macchina a stati ---

    public void Place(DateTimeOffset timestamp)
    {
        EnsureStatus(OrderStatus.Draft);
        DomainGuard.Against(_lines.Count == 0, "Non si può confermare un ordine senza righe.");
        Transition(OrderStatus.Placed);
        PlacedAt = timestamp;
        RaiseDomainEvent(new OrderPlacedDomainEvent(Id, OrderNumber, TotalAmount.Amount, Currency));
    }

    public void Confirm()
    {
        EnsureStatus(OrderStatus.Placed);
        Transition(OrderStatus.Confirmed);
    }

    public void MarkAsPaid(DateTimeOffset timestamp)
    {
        DomainGuard.Against(
            Status is not (OrderStatus.Placed or OrderStatus.Confirmed),
            $"Un ordine in stato {Status} non può essere pagato.");
        Transition(OrderStatus.Paid);
        PaidAt = timestamp;
    }

    public void Ship(DateTimeOffset timestamp)
    {
        EnsureStatus(OrderStatus.Paid);
        Transition(OrderStatus.Shipped);
        ShippedAt = timestamp;
    }

    public void Deliver(DateTimeOffset timestamp)
    {
        EnsureStatus(OrderStatus.Shipped);
        Transition(OrderStatus.Delivered);
        DeliveredAt = timestamp;
    }

    public void Cancel(string reason, DateTimeOffset timestamp)
    {
        DomainGuard.Against(
            Status is OrderStatus.Shipped or OrderStatus.Delivered or OrderStatus.Cancelled or OrderStatus.Refunded,
            $"Un ordine in stato {Status} non può essere annullato.");
        Transition(OrderStatus.Cancelled);
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? "N/D" : reason.Trim();
        CancelledAt = timestamp;
    }

    public void AddTag(string tag)
    {
        DomainGuard.Against(string.IsNullOrWhiteSpace(tag), "Il tag non può essere vuoto.");
        var normalized = tag.Trim().ToLowerInvariant();
        if (!_tags.Contains(normalized))
            _tags.Add(normalized);
    }

    // --- Helpers privati ---

    private void RecalculateTotal()
    {
        var total = Money.Zero(Currency);
        foreach (var line in _lines)
            total = total.Add(line.LineTotal);
        TotalAmount = total;
    }

    private OrderLine FindLine(Guid lineId) =>
        _lines.FirstOrDefault(l => l.Id == lineId)
        ?? throw new DomainException("Riga d'ordine non trovata.");

    private void EnsureMutable() =>
        DomainGuard.Against(
            Status is not OrderStatus.Draft,
            $"Le righe non sono modificabili in stato {Status}.");

    private void EnsureStatus(OrderStatus expected) =>
        DomainGuard.Against(
            Status != expected,
            $"L'operazione richiede lo stato {expected}, ma l'ordine è in {Status}.");

    private void Transition(OrderStatus next)
    {
        var previous = Status;
        Status = next;
        RaiseDomainEvent(new OrderStatusChangedDomainEvent(Id, previous, next));
    }
}
