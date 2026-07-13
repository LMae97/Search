using Search.Application.Querying.Metadata;
using Search.Domain.Ordering.Orders;

namespace Search.Application.Maps;

/// <summary>
/// Mappa di ricerca dell'ordine (documento MongoDB). È dichiarata esattamente come quella del
/// prodotto: gli stessi selettori valgono sia per il translator LINQ sia per quello Mongo.
/// I value object embedded (Customer, ShippingAddress, TotalAmount) diventano più campi
/// proiettabili tramite selettori annidati.
/// </summary>
public sealed class OrderSearchMap : EntitySearchMap<Order>
{
    public override string EntityName => "order";

    public OrderSearchMap()
    {
        MapField("id", o => o.Id);
        MapField("orderNumber", o => o.OrderNumber);
        MapField("status", o => o.Status);
        MapField("currency", o => o.Currency);

        // TotalAmount è un value object -> importo proiettabile a parte.
        MapField("total", o => o.TotalAmount.Amount);

        // Cliente embedded
        MapField("customerId", o => o.Customer.CustomerId);
        MapField("customerName", o => o.Customer.FullName);
        MapField("customerEmail", o => o.Customer.Email);

        // Indirizzo di spedizione embedded
        MapField("shippingCity", o => o.ShippingAddress.City);
        MapField("shippingCountry", o => o.ShippingAddress.Country);

        // Date del ciclo di vita
        MapField("placedAt", o => o.PlacedAt);
        MapField("paidAt", o => o.PaidAt);

        // Array
        MapArray("tags", o => o.Tags);

        // Audit / soft delete
        MapField("createdAt", o => o.CreatedAt);
        MapField("createdBy", o => o.CreatedBy);
        MapField("isDeleted", o => o.IsDeleted);
    }
}
