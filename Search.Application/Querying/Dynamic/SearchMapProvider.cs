using Search.Application.Querying.Authorization;
using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Dynamic;

/// <summary>
/// Costruisce la mappa <b>effettiva</b> per una richiesta: unisce i campi statici (codice) con
/// quelli dinamici del tenant (<see cref="SearchCaller.SpaceId"/>), poi applica il filtro per
/// permesso (<see cref="EffectiveSearchMap"/>). È l'unico punto che conosce l'utente corrente.
/// </summary>
public sealed class SearchMapProvider
{
    private readonly IReadOnlyDictionary<string, IEntitySearchMap> _staticMaps;
    private readonly IDynamicFieldProvider _dynamicFields;

    public SearchMapProvider(IEnumerable<IEntitySearchMap> staticMaps, IDynamicFieldProvider dynamicFields)
    {
        _staticMaps = staticMaps.ToDictionary(map => map.EntityName, map => map, StringComparer.OrdinalIgnoreCase);
        _dynamicFields = dynamicFields;
    }

    public IEntitySearchMap GetEffectiveMap(string entityName, SearchCaller caller)
    {
        if (!_staticMaps.TryGetValue(entityName, out var staticMap))
            throw new InvalidOperationException($"Nessuna mappa di ricerca registrata per '{entityName}'.");

        var dynamicDescriptors = _dynamicFields
            .GetFields(entityName, caller.SpaceId)
            .Select(DynamicFieldFactory.ToDescriptor);

        var merged = new MaterializedSearchMap(
            staticMap.EntityName,
            staticMap.Fields.Values.Concat(dynamicDescriptors));

        return new EffectiveSearchMap(merged, caller);
    }
}
