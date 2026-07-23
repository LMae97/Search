using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Metadata;

namespace Search.Tests;

/// <summary>
/// Helper condivisi ai test. Punto chiave: la mappa effettiva si costruisce passando per il
/// <b>resolver reale</b> (<see cref="DbBackedSearchMapProvider"/>), ma da definizioni create su misura
/// per il singolo test — così i test esercitano il motore vero e <b>non dipendono dal seed globale</b>
/// (<c>SimulatedFieldDefinitionDatabase</c>), che domani sarà sostituito da un provider su DB.
/// </summary>
internal static class TestSupport
{
    public static readonly Guid Space = Guid.Parse("4ae7781f-28a9-4070-b545-dfeb854c8764");

    public static SearchCaller Caller(params Guid[] permissions)
        => new(Space, new HashSet<Guid>(permissions));

    public static IEntitySearchMap Map(SearchEntity entity, SearchCaller caller, params SearchFieldDefinition[] defs)
    {
        var provider = new InMemorySearchFieldDefinitionProvider();
        foreach (var def in defs) provider.Add(def);

        return new DbBackedSearchMapProvider(provider).GetEffectiveMap(entity, caller);
    }

    /// <summary>Scorciatoia per una definizione di campo (l'ordine posizionale del record è verboso).</summary>
    public static SearchFieldDefinition Def(
        string entity, string name, FieldKind kind, string path,
        bool isArray = false, bool json = false, int? defaultOrder = null, Guid? permission = null)
        => new(entity, name, kind, json, isArray, path,
            DefaultOrder: defaultOrder, RequiredPermissionId: permission);
}

/// <summary>Entità CLR di prova per il path selector-based (store EF / esecuzione LINQ in-memory).</summary>
internal sealed class TestProduct
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public List<TestTag> Tags { get; set; } = [];
}

internal sealed class TestTag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}
