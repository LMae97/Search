using Search.Domain.Common;

namespace Search.Domain.Ordering.Orders;

public sealed record OrderPlacedDomainEvent(
    Guid OrderId,
    string OrderNumber,
    decimal Total,
    string Currency) : DomainEvent;

public sealed record OrderStatusChangedDomainEvent(
    Guid OrderId,
    OrderStatus PreviousStatus,
    OrderStatus NewStatus) : DomainEvent;
