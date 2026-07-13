namespace Search.Domain.Common.ValueObjects;

/// <summary>
/// Importo monetario: coppia (importo, valuta ISO 4217). Immutabile; le operazioni
/// aritmetiche restituiscono nuove istanze e vietano di mischiare valute diverse.
/// </summary>
public sealed class Money : ValueObject
{
    public decimal Amount { get; }

    /// <summary>Codice valuta ISO 4217 in maiuscolo (es. "EUR").</summary>
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Of(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new DomainException($"Codice valuta ISO 4217 non valido: '{currency}'.");
        if (amount < 0)
            throw new DomainException("L'importo non può essere negativo.");
        return new Money(decimal.Round(amount, 2, MidpointRounding.ToEven), currency.Trim().ToUpperInvariant());
    }

    public static Money Zero(string currency) => Of(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        var result = Amount - other.Amount;
        if (result < 0)
            throw new DomainException("Il risultato non può essere negativo.");
        return new Money(result, Currency);
    }

    public Money MultiplyBy(int quantity)
    {
        if (quantity < 0)
            throw new DomainException("La quantità non può essere negativa.");
        return new Money(Amount * quantity, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException($"Valute incompatibili: {Currency} vs {other.Currency}.");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
