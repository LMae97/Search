using Search.Domain.Common;

namespace Search.Domain.Ordering.Orders.ValueObjects;

/// <summary>
/// Snapshot dei dati cliente al momento dell'ordine (denormalizzato). Il cliente "vero"
/// vive in un altro contesto: qui teniamo solo ciò che serve all'ordine, per Id + dati minimi.
/// </summary>
public sealed class CustomerInfo : ValueObject
{
    public Guid CustomerId { get; }
    public string FullName { get; }
    public string Email { get; }

    private CustomerInfo(Guid customerId, string fullName, string email)
    {
        CustomerId = customerId;
        FullName = fullName;
        Email = email;
    }

    public static CustomerInfo Create(Guid customerId, string fullName, string email)
    {
        DomainGuard.Against(customerId == Guid.Empty, "Il CustomerId è obbligatorio.");
        DomainGuard.Against(string.IsNullOrWhiteSpace(fullName), "Il nome del cliente è obbligatorio.");
        DomainGuard.Against(
            string.IsNullOrWhiteSpace(email) || !email.Contains('@'),
            "È richiesta un'email valida.");

        return new CustomerInfo(customerId, fullName.Trim(), email.Trim().ToLowerInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CustomerId;
        yield return FullName;
        yield return Email;
    }
}
