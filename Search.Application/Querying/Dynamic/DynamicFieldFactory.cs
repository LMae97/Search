using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Dynamic;

/// <summary>Converte una <see cref="DynamicFieldDefinition"/> in un <see cref="FieldDescriptor"/> dinamico.</summary>
public static class DynamicFieldFactory
{
    public static FieldDescriptor ToDescriptor(DynamicFieldDefinition definition)
    {
        var clrType = ClrTypeFor(definition.Kind);
        var operators = OperatorRules.DefaultFor(definition.Kind, definition.IsArray);

        // selector: null → è un campo dinamico; il path esplicito guida i translator che lo supportano (Mongo).
        return new FieldDescriptor(definition.Name, definition.Kind, definition.IsArray, clrType, selector: null, operators)
        {
            StoragePath = definition.StoragePath,
            Label = definition.Label ?? definition.Name,
            Section = definition.Section,
            RequiredPermissionId = definition.RequiredPermissionId
        };
    }

    // Tipo CLR usato solo per la coercizione dei valori del filtro (non esiste una proprietà reale).
    private static Type ClrTypeFor(FieldKind kind) => kind switch
    {
        FieldKind.String => typeof(string),
        FieldKind.Integer => typeof(long),
        FieldKind.Decimal => typeof(decimal),
        FieldKind.Boolean => typeof(bool),
        FieldKind.DateTime => typeof(DateTimeOffset),
        FieldKind.Guid => typeof(Guid),
        FieldKind.Enum => typeof(string), // un "enum" dinamico non ha tipo CLR: lo trattiamo come stringa
        _ => typeof(string)
    };
}
