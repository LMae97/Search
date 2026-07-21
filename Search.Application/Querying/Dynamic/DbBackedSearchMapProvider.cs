using Search.Application.Querying.Authorization;
using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Dynamic;

/// <summary>
/// Costruisce la mappa effettiva per una richiesta a partire dalle <see cref="SearchFieldDefinition"/>
/// "a DB": carica le definizioni (globali + del tenant), le risolve in <see cref="FieldDescriptor"/>,
/// le unisce e applica il filtro per permesso. Rimpiazza le mappe in codice come sorgente dei campi;
/// il resto del motore (validazione, sanitizer, translator) resta invariato perché lavora su
/// <see cref="IEntitySearchMap"/>.
/// </summary>
public sealed class DbBackedSearchMapProvider
{
    private readonly ISearchFieldDefinitionProvider _definitions;

    public DbBackedSearchMapProvider(ISearchFieldDefinitionProvider definitions)
    {
        _definitions = definitions;
    }

    public IEntitySearchMap GetEffectiveMap(SearchEntity entity, SearchCaller caller)
    {
        var resolver = new SearchFieldDefinitionResolver(entity);

        var descriptors = _definitions
            .GetDefinitions(entity.Name, caller.SpaceId)
            .Select(resolver.Resolve);

        //Costruisce la mappa (filtrando per permessi)
        return EffectiveSearchMap.For(entity.Name, descriptors, caller);
    }
}
