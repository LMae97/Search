namespace Search.Application.Querying.Dynamic;

/// <summary>
/// Registrazione di un'entità ricercabile: nome pubblico, tipo di store e — per il relazionale —
/// il tipo CLR necessario a ricostruire i selettori dai path.
/// </summary>
public sealed record SearchEntity(string Name, StoreKind Store, Type? ClrType)
{
    public static SearchEntity RelationalEF<T>(string name) => new(name, StoreKind.PostgresEF, typeof(T));
    public static SearchEntity RelationalRaw<T>(string name) => new(name, StoreKind.PostgresRaw, typeof(T));

    public static SearchEntity Document(string name) => new(name, StoreKind.Mongo, ClrType: null);
}
