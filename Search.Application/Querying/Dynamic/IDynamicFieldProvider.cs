namespace Search.Application.Querying.Dynamic;

/// <summary>
/// Fornisce le definizioni dei campi dinamici per una data entità e un dato tenant.
/// L'implementazione reale leggerà da DB; il contratto resta indipendente dalla persistenza.
/// </summary>
public interface IDynamicFieldProvider
{
    IReadOnlyList<DynamicFieldDefinition> GetFields(string entityName, Guid spaceId);
}
