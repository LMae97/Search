namespace Search.Application.Querying.Metadata;

/// <summary>Categoria di tipo di un campo. Determina gli operatori ammessi di default.</summary>
public enum FieldKind
{
    String,
    Integer,
    Decimal,
    Boolean,
    DateTime,
    Guid,
    Enum
}
