namespace WeByte.Search.Core.Dynamic;

/// <summary>
/// Registrazione di un'entità ricercabile: nome pubblico, tipo di store e — solo per <see cref="StoreKind.PostgresEF"/> —
/// il tipo CLR necessario a ricostruire i selettori (Expression) dai path. Per PostgresRaw e Mongo il tipo CLR
/// non serve (il binding avviene via StoragePath), quindi è null.
/// </summary>
public sealed record SearchEntity(string Name, Guid ContextId, StoreKind Store, Type? ClrType)
{
    /// <summary>Store EF: il path è una property-path CLR → serve il tipo per ricostruire il selettore.</summary>
    public static SearchEntity RelationalEF<T>(string name, Guid contextId) => new(name, contextId, StoreKind.PostgresEF, typeof(T));

    /// <summary>Store SQL grezzo: il path è un'espressione-colonna SQL (StoragePath) → nessun tipo CLR necessario.</summary>
    public static SearchEntity RelationalRaw(string name, Guid contextId) => new(name, contextId, StoreKind.PostgresRaw, ClrType: null);

    /// <summary>Store documentale (Mongo): il path è il path del documento (StoragePath) → nessun tipo CLR.</summary>
    public static SearchEntity Document(string name, Guid contextId) => new(name, contextId, StoreKind.Mongo, ClrType: null);
}
