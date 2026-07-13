using Search.Domain.Common;

namespace Search.Domain.Catalog.Brands;

public sealed record BrandCreatedDomainEvent(Guid BrandId, string Code, string Name) : DomainEvent;

public sealed record BrandDeactivatedDomainEvent(Guid BrandId, string Code) : DomainEvent;
