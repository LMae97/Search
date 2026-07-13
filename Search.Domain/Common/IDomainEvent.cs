namespace Search.Domain.Common;

/// <summary>
/// Fatto di dominio già accaduto (nome al passato: <c>OrderPlaced</c>, <c>ProductPriceChanged</c>).
/// Viene accumulato sull'aggregato e pubblicato dall'infrastruttura dopo il commit
/// della transazione (outbox pattern), così i side-effect non inquinano il dominio.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredOn { get; }
}

/// <summary>Base comoda: imposta <see cref="OccurredOn"/> al momento della creazione dell'evento.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}
