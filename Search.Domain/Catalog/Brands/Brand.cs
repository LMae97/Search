using Search.Domain.Common;

namespace Search.Domain.Catalog.Brands;

/// <summary>
/// Marca/produttore a catalogo. Aggregato radice persistito su PostgreSQL.
/// Il <see cref="Code"/> è la chiave di business (slug univoco), l'<c>Id</c> è la chiave tecnica.
/// </summary>
public sealed class Brand : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;

    /// <summary>Slug univoco stabile (es. "acme-corp"). Chiave naturale usata nelle URL.</summary>
    public string Code { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Paese d'origine, ISO 3166-1 alpha-2 (es. "IT").</summary>
    public string? CountryOfOrigin { get; private set; }

    public Uri? Website { get; private set; }
    public Uri? LogoUrl { get; private set; }
    public bool IsActive { get; private set; }

    private Brand()
    {
        // Riservato all'ORM.
    }

    private Brand(Guid id, string name, string code) : base(id)
    {
        Name = name;
        Code = code;
        IsActive = true;
    }

    /// <summary>Factory: unico modo per creare una <see cref="Brand"/> valida.</summary>
    public static Brand Create(
        string name,
        string code,
        string? description = null,
        string? countryOfOrigin = null,
        Uri? website = null,
        Uri? logoUrl = null)
    {
        var brand = new Brand(Guid.NewGuid(), NormalizeName(name), NormalizeCode(code))
        {
            Description = description?.Trim(),
            CountryOfOrigin = NormalizeCountry(countryOfOrigin),
            Website = website,
            LogoUrl = logoUrl
        };
        brand.RaiseDomainEvent(new BrandCreatedDomainEvent(brand.Id, brand.Code, brand.Name));
        return brand;
    }

    public void Rename(string newName)
    {
        var normalized = NormalizeName(newName);
        if (normalized == Name)
            return;
        Name = normalized;
    }

    public void UpdateDetails(string? description, string? countryOfOrigin, Uri? website, Uri? logoUrl)
    {
        Description = description?.Trim();
        CountryOfOrigin = NormalizeCountry(countryOfOrigin);
        Website = website;
        LogoUrl = logoUrl;
    }

    public void Activate()
    {
        if (IsActive)
            return;
        IsActive = true;
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;
        IsActive = false;
        RaiseDomainEvent(new BrandDeactivatedDomainEvent(Id, Code));
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Il nome della marca è obbligatorio.");
        var trimmed = name.Trim();
        if (trimmed.Length > 200)
            throw new DomainException("Il nome della marca non può superare i 200 caratteri.");
        return trimmed;
    }

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Il codice della marca è obbligatorio.");
        var normalized = code.Trim().ToLowerInvariant().Replace(' ', '-');
        if (normalized.Length is < 2 or > 50)
            throw new DomainException("Il codice della marca deve essere tra 2 e 50 caratteri.");
        return normalized;
    }

    private static string? NormalizeCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
            return null;
        var normalized = country.Trim().ToUpperInvariant();
        if (normalized.Length != 2)
            throw new DomainException("Il paese deve essere un codice ISO 3166-1 alpha-2.");
        return normalized;
    }
}
