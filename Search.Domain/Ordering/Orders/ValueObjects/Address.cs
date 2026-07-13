using Search.Domain.Common;

namespace Search.Domain.Ordering.Orders.ValueObjects;

/// <summary>Indirizzo postale. Value object immutabile, embedded nel documento ordine (Mongo).</summary>
public sealed class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string Country { get; }

    public string? State { get; }

    private Address(string street, string city, string postalCode, string country, string? state)
    {
        Street = street;
        City = city;
        PostalCode = postalCode;
        Country = country;
        State = state;
    }

    public static Address Create(string street, string city, string postalCode, string country, string? state = null)
    {
        DomainGuard.Against(string.IsNullOrWhiteSpace(street), "La via è obbligatoria.");
        DomainGuard.Against(string.IsNullOrWhiteSpace(city), "La città è obbligatoria.");
        DomainGuard.Against(string.IsNullOrWhiteSpace(postalCode), "Il CAP è obbligatorio.");
        DomainGuard.Against(
            string.IsNullOrWhiteSpace(country) || country.Trim().Length != 2,
            "Il paese deve essere un codice ISO 3166-1 alpha-2.");

        return new Address(
            street.Trim(),
            city.Trim(),
            postalCode.Trim(),
            country.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(state) ? null : state.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return PostalCode;
        yield return Country;
        yield return State;
    }
}
