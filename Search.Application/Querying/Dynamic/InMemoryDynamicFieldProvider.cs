namespace Search.Application.Querying.Dynamic;

/// <summary>Implementazione in memoria di <see cref="IDynamicFieldProvider"/> (per demo/test).</summary>
public sealed class InMemoryDynamicFieldProvider : IDynamicFieldProvider
{
    private readonly List<DynamicFieldDefinition> _definitions = [];

    public void Add(DynamicFieldDefinition definition) => _definitions.Add(definition);

    public IReadOnlyList<DynamicFieldDefinition> GetFields(string entityName, Guid spaceId)
        => _definitions
            .Where(d => d.SpaceId == spaceId && d.EntityName.Equals(entityName, StringComparison.OrdinalIgnoreCase))
            .ToList();
}
