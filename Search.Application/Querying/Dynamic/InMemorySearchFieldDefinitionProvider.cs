namespace Search.Application.Querying.Dynamic;

/// <summary>Implementazione in memoria di <see cref="ISearchFieldDefinitionProvider"/> (demo/test).</summary>
public sealed class InMemorySearchFieldDefinitionProvider : ISearchFieldDefinitionProvider
{
    private readonly List<SearchFieldDefinition> _definitions = [];

    public void Add(SearchFieldDefinition definition) => _definitions.Add(definition);

    public IReadOnlyList<SearchFieldDefinition> GetDefinitions(string entityName, Guid spaceId)
        => _definitions
            .Where(d => d.EntityName.Equals(entityName, StringComparison.OrdinalIgnoreCase)
                        && (d.SpaceId is null || d.SpaceId == spaceId))
            .ToList();
}
