namespace Search.Domain.Common;

/// <summary>
/// Base per i value object: nessuna identità, uguaglianza <b>strutturale</b> (per valore)
/// e immutabilità. Due value object sono uguali se lo sono tutte le loro componenti.
/// </summary>
public abstract class ValueObject
{
    /// <summary>Componenti che definiscono l'identità di valore dell'oggetto.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;
        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in GetEqualityComponents())
            hash.Add(component);
        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}
