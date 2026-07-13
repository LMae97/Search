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
    private readonly SearchFieldDefinitionResolver _resolver;

    public DbBackedSearchMapProvider(ISearchFieldDefinitionProvider definitions, SearchFieldDefinitionResolver resolver)
    {
        _definitions = definitions;
        _resolver = resolver;
    }

    public IEntitySearchMap GetEffectiveMap(string entityName, SearchCaller caller)
    {
        var descriptors = _definitions
            .GetDefinitions(entityName, caller.SpaceId)
            .Select(_resolver.Resolve);

        return EffectiveSearchMap.For(entityName, descriptors, caller);
    }
}
