namespace Search.Application.Querying.Dynamic;

/// <summary>Registro delle entità ricercabili: nome → <see cref="SearchEntity"/>.</summary>
public sealed class SearchEntityRegistry
{
    private readonly Dictionary<string, SearchEntity> _entities;

    public SearchEntityRegistry(IEnumerable<SearchEntity> entities)
        => _entities = entities.ToDictionary(entity => entity.Name, entity => entity, StringComparer.OrdinalIgnoreCase);

    public SearchEntity Get(string entityName)
        => _entities.TryGetValue(entityName, out var entity)
            ? entity
            : throw new InvalidOperationException($"Entità di ricerca non registrata: '{entityName}'.");
}
