using Search.Domain.Common;

namespace Search.Domain.Catalog.Products.ValueObjects;

/// <summary>Ingombro fisico del prodotto, in millimetri.</summary>
public sealed class Dimensions : ValueObject
{
    public decimal LengthMm { get; }
    public decimal WidthMm { get; }
    public decimal HeightMm { get; }

    private Dimensions(decimal lengthMm, decimal widthMm, decimal heightMm)
    {
        LengthMm = lengthMm;
        WidthMm = widthMm;
        HeightMm = heightMm;
    }

    public static Dimensions Create(decimal lengthMm, decimal widthMm, decimal heightMm)
    {
        if (lengthMm <= 0 || widthMm <= 0 || heightMm <= 0)
            throw new DomainException("Tutte le dimensioni devono essere positive.");
        return new Dimensions(lengthMm, widthMm, heightMm);
    }

    /// <summary>Volume calcolato (mm³) — proprietà derivata, utile come campo proiettabile.</summary>
    public decimal VolumeMm3 => LengthMm * WidthMm * HeightMm;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return LengthMm;
        yield return WidthMm;
        yield return HeightMm;
    }
}
