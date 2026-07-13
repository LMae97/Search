using Search.Domain.Common;

namespace Search.Domain.Catalog.Products.ValueObjects;

/// <summary>
/// Stock Keeping Unit: codice identificativo del prodotto. Normalizzato (trim + maiuscolo)
/// per evitare duplicati logici ("abc-1" e " ABC-1 ").
/// </summary>
public sealed class Sku : ValueObject
{
    public string Value { get; }

    private Sku(string value) => Value = value;

    public static Sku Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Lo SKU non può essere vuoto.");
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length is < 3 or > 64)
            throw new DomainException("La lunghezza dello SKU deve essere tra 3 e 64 caratteri.");
        return new Sku(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
