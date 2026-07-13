namespace Search.Domain.Common;

/// <summary>
/// Radice di aggregato: è il confine di consistenza transazionale e l'unico punto
/// d'ingresso per modificare l'aggregato. Colleziona i domain event da pubblicare
/// dopo il salvataggio.
/// </summary>
public abstract class AggregateRoot<TKey> : AuditableEntity<TKey>
    where TKey : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Eventi in attesa di pubblicazione. Sola lettura verso l'esterno.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    /// <summary>Svuota la coda. Chiamato dall'infrastruttura dopo aver dispatchato gli eventi.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    protected AggregateRoot(TKey id) : base(id)
    {
    }

    protected AggregateRoot()
    {
    }
}
