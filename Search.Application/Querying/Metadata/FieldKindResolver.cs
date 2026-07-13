namespace Search.Application.Querying.Metadata;

/// <summary>
/// Deriva la categoria di tipo (<see cref="FieldKind"/>) e il tipo CLR sottostante (nullable scartato)
/// da un <see cref="Type"/>. Usato sia dalle mappe in codice sia dalle mappe costruite da path a DB.
/// </summary>
public static class FieldKindResolver
{
    public static (FieldKind Kind, Type Underlying) Resolve(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying.IsEnum) return (FieldKind.Enum, underlying);
        if (underlying == typeof(string)) return (FieldKind.String, underlying);
        if (underlying == typeof(bool)) return (FieldKind.Boolean, underlying);
        if (underlying == typeof(Guid)) return (FieldKind.Guid, underlying);
        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(DateOnly))
            return (FieldKind.DateTime, underlying);
        if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short) || underlying == typeof(byte))
            return (FieldKind.Integer, underlying);
        if (underlying == typeof(decimal) || underlying == typeof(double) || underlying == typeof(float))
            return (FieldKind.Decimal, underlying);

        throw new NotSupportedException($"Tipo di campo non supportato per la ricerca: {type.Name}.");
    }
}
