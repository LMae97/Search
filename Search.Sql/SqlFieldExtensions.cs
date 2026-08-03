using WeByte.Search.Core.Metadata;

namespace WeByte.Search.Sql;

/// <summary>Helper condivisi tra il translator e il query builder dello store SQL grezzo (PostgresRaw).</summary>
internal static class SqlFieldExtensions
{
    /// <summary>
    /// Espressione-colonna SQL del campo, presa dallo <see cref="FieldDescriptor.StoragePath"/> (fidata perché
    /// da whitelist). Unico punto: stesso messaggio d'errore per translator e builder.
    /// </summary>
    public static string SqlColumn(this FieldDescriptor field) =>
        field.StoragePath
        ?? throw new NotSupportedException($"Il campo '{field.Name}' non ha una colonna SQL (StoragePath).");

    /// <summary>Espressione-colonna SQL del riferimento (value) di un campo <see cref="FieldKind.Link"/>.</summary>
    public static string LinkColumn(this FieldDescriptor field) =>
        field.LinkReferencePath
        ?? throw new NotSupportedException($"Il campo '{field.Name}' non ha una colonna di riferimento (LinkReferencePath).");

    /// <summary>Espressione-colonna SQL del valore secondario (vedi <see cref="FieldDescriptor.SecondaryStoragePath"/>).</summary>
    public static string SecondaryColumn(this FieldDescriptor field) =>
        field.SecondaryStoragePath
        ?? throw new NotSupportedException($"Il campo '{field.Name}' non ha una colonna secondaria (SecondaryStoragePath).");
}
