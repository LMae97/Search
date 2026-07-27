using Search.Application.Querying.Metadata;

namespace Search.Infrastructure.Sql;

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
}
