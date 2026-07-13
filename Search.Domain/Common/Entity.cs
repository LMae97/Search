namespace Search.Domain.Common;

/// <summary>
/// Base per le entità di dominio. L'uguaglianza è per <b>identità</b> (stesso tipo + stesso Id),
/// non per valore: due entità con lo stesso Id rappresentano lo stesso oggetto di business,
/// anche se qualche proprietà differisce. Le entità "transient" (Id ancora di default,
/// non persistite) non sono mai uguali tra loro.
/// </summary>
public abstract class Entity<TKey> : IEntity<TKey>
    where TKey : notnull
{
    /// <summary>Identità dell'entità. Il setter è protetto: lo assegna il costruttore o l'ORM.</summary>
    public TKey Id { get; protected set; } = default!;

    protected Entity(TKey id) => Id = id;

    /// <summary>Costruttore senza parametri richiesto da ORM/serializzatori per la materializzazione.</summary>
    protected Entity()
    {
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TKey> other)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        // Un proxy dell'ORM potrebbe avere un tipo derivato: confrontiamo il tipo "reale".
        if (GetType() != other.GetType())
            return false;
        if (IsTransient(this) || IsTransient(other))
            return false;
        return EqualityComparer<TKey>.Default.Equals(Id, other.Id);
    }

    private static bool IsTransient(Entity<TKey> entity) =>
        EqualityComparer<TKey>.Default.Equals(entity.Id, default!);

    public override int GetHashCode() =>
        EqualityComparer<TKey>.Default.GetHashCode(Id);

    public static bool operator ==(Entity<TKey>? left, Entity<TKey>? right) =>
        Equals(left, right);

    public static bool operator !=(Entity<TKey>? left, Entity<TKey>? right) =>
        !Equals(left, right);
}
