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
    Enum,
    ObjectId    // id documentale (Mongo): 24 hex; coerciato come stringa, tradotto in BsonObjectId dal translator Mongo
}
