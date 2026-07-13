using Search.Domain.Common;

namespace Search.Domain.Catalog.Products;

public sealed record ProductPriceChangedDomainEvent(
    Guid ProductId,
    decimal OldAmount,
    decimal NewAmount,
    string Currency) : DomainEvent;

public sealed record ProductDiscontinuedDomainEvent(Guid ProductId) : DomainEvent;
