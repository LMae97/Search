using Search.Domain.Common;

namespace Search.Domain.Catalog.Tags;

/// <summary>
/// Etichetta riutilizzabile del catalogo. È un aggregato radice a sé: lo stesso tag è condiviso
/// tra più prodotti (relazione molti-a-molti). <see cref="Slug"/> è la chiave naturale normalizzata
/// (utile per l'unicità), <see cref="Name"/> è l'etichetta mostrata.
/// </summary>
public sealed class Tag : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    private Tag()
    {
        // Riservato all'ORM.
    }

    private Tag(Guid id, string name, string slug) : base(id)
    {
        Name = name;
        Slug = slug;
    }

    public static Tag Create(string name)
    {
        var (normalizedName, slug) = Normalize(name);
        return new Tag(Guid.NewGuid(), normalizedName, slug);
    }

    public void Rename(string newName)
    {
        var (normalizedName, slug) = Normalize(newName);
        Name = normalizedName;
        Slug = slug;
    }

    private static (string Name, string Slug) Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Il nome del tag è obbligatorio.");
        var trimmed = name.Trim();
        if (trimmed.Length > 50)
            throw new DomainException("Il tag non può superare i 50 caratteri.");
        var slug = trimmed.ToLowerInvariant().Replace(' ', '-');
        return (trimmed, slug);
    }
}
