using System.Diagnostics.CodeAnalysis;

namespace Search.Application.Querying.Metadata;

/// <summary>
/// Mappa "materializzata" da un insieme di descrittori già pronti (non da selettori in codice).
/// Serve a combinare campi statici (codice) e dinamici (per tenant) in un'unica mappa.
/// In caso di nomi duplicati vince il primo inserito (i campi statici, passati per primi).
/// </summary>
public sealed class MaterializedSearchMap : IEntitySearchMap
{
    private readonly Dictionary<string, FieldDescriptor> _fields;

    public string EntityName { get; }

    public MaterializedSearchMap(string entityName, IEnumerable<FieldDescriptor> fields)
    {
        EntityName = entityName;
        _fields = new Dictionary<string, FieldDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
            _fields.TryAdd(field.Name, field);
    }

    public IReadOnlyDictionary<string, FieldDescriptor> Fields => _fields;

    public bool TryGetField(string name, [MaybeNullWhen(false)] out FieldDescriptor descriptor)
        => _fields.TryGetValue(name, out descriptor);
}
