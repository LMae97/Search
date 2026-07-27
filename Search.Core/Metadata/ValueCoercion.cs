using System.Globalization;

namespace Search.Application.Querying.Linq;

/// <summary>
/// Converte i valori del filtro (che dal JSON arrivano come string/number/bool) verso il tipo
/// CLR atteso dal campo: enum da stringa, Guid, date in UTC, numeri con cultura invariante.
/// </summary>
public static class ValueCoercion
{
    public static object? Coerce(object? value, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is null)
            return null;
        if (underlying.IsInstanceOfType(value))
            return value;

        if (underlying.IsEnum)
            return value is string enumName
                ? Enum.Parse(underlying, enumName, ignoreCase: true)
                : Enum.ToObject(underlying, value);

        if (underlying == typeof(Guid))
            return value is Guid g ? g : Guid.Parse(value.ToString()!);

        if (underlying == typeof(DateTimeOffset))
            return value is DateTimeOffset dto
                ? dto
                : DateTimeOffset.Parse(value.ToString()!, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        if (underlying == typeof(DateTime))
            return value is DateTime dt
                ? dt
                : DateTime.Parse(value.ToString()!, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        if (underlying == typeof(DateOnly))
            return value is DateOnly d ? d : DateOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture);

        if (underlying == typeof(string))
            return value.ToString();

        return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
    }
}
