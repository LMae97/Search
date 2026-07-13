namespace Search.Domain.Ordering.Orders;

/// <summary>Stati dell'ordine. Le transizioni valide sono imposte dai metodi di <c>Order</c>.</summary>
public enum OrderStatus
{
    Draft = 0,
    Placed = 1,
    Confirmed = 2,
    Paid = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6,
    Refunded = 7
}
