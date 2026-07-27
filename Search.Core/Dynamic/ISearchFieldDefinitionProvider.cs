namespace Search.Application.Querying.Dynamic;

/// <summary>
/// Fornisce le definizioni dei campi per un'entità e un tenant: quelle globali (SpaceId null)
/// più quelle specifiche del tenant. L'implementazione reale legge da DB.
/// </summary>
public interface ISearchFieldDefinitionProvider
{
    IReadOnlyList<SearchFieldDefinition> GetDefinitions(string entityName, Guid spaceId);
}
