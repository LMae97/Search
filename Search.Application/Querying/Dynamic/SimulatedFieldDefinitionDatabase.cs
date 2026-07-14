using Search.Application.Querying.Authorization;
using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Dynamic;

/// <summary>
/// Finto "database" delle definizioni dei campi: al posto di una tabella EF, tiene le righe in memoria.
/// Condiviso tra la demo console e l'API. Domani si rimpiazza con un provider su EF/DbContext:
/// il resto dipende solo da <see cref="ISearchFieldDefinitionProvider"/>.
/// </summary>
public sealed class SimulatedFieldDefinitionDatabase : ISearchFieldDefinitionProvider
{
    /// <summary>Tenant di demo a cui è associato il campo dinamico "deliveryZone".</summary>
    public static readonly Guid DemoSpace = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly InMemorySearchFieldDefinitionProvider _rows = new();

    public SimulatedFieldDefinitionDatabase()
    {
        // === product (store relazionale): Path = property-path CLR ===
        _rows.Add(new SearchFieldDefinition("product", "name", FieldKind.String, false, "Name"));
        _rows.Add(new SearchFieldDefinition("product", "price", FieldKind.Decimal, false, "Price.Amount",
            Label: "Prezzo", Section: "Economici", RequiredPermissionId: SearchPermissions.ViewPrice));
        _rows.Add(new SearchFieldDefinition("product", "status", FieldKind.Enum, false, "Status"));
        // collezione molti-a-molti → il path "Tags.Name" verrà ricostruito in x.Tags.Select(t => t.Name)
        _rows.Add(new SearchFieldDefinition("product", "tags", FieldKind.String, true, "Tags.Name", Label: "Tag"));
        _rows.Add(new SearchFieldDefinition("product", "tagIds", FieldKind.Guid, true, "Tags.Id"));

        // === order (store documentale): Path = path del documento Mongo ===
        _rows.Add(new SearchFieldDefinition("order", "status", FieldKind.Enum, false, "status"));
        _rows.Add(new SearchFieldDefinition("order", "total", FieldKind.Decimal, false, "totalAmount.amount"));
        _rows.Add(new SearchFieldDefinition("order", "customerEmail", FieldKind.String, false, "customer.email"));

        // === campo dinamico, solo per il tenant DemoSpace ===
        _rows.Add(new SearchFieldDefinition("order", "deliveryZone", FieldKind.String, false, "attributes.deliveryZone",
            Label: "Zona di consegna", Section: "Logistica", SpaceId: DemoSpace));
    }

    public IReadOnlyList<SearchFieldDefinition> GetDefinitions(string entityName, Guid spaceId)
        => _rows.GetDefinitions(entityName, spaceId);
}
