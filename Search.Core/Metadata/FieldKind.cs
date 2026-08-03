namespace WeByte.Search.Core.Metadata;

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
    Link,
    ObjectId,   // id documentale (Mongo): 24 hex; coerciato come stringa, tradotto in BsonObjectId dal translator Mongo

    /// <summary>
    /// Campo applicativo/di presentazione, non un vero dato ricercabile: un pulsante d'azione della UI
    /// (es. "impersonifica utente", "duplica prodotto") o un'immagine. Il valore sottostante è sempre un
    /// Guid o una stringa (l'id della riga, un path), ma non ha senso filtrarlo, ordinarlo o esportarlo —
    /// per questo un'unica categoria basta al motore, a differenza del vecchio sistema che aveva un GUID
    /// per ogni singolo tipo di pulsante: quella distinzione serve solo al FE per scegliere il widget, e la
    /// ricava già dal <b>nome</b> del campo (es. "btnImpersonateUser"), non da qui.
    /// <para>
    /// <see cref="OperatorRules"/> non ammette alcun operatore su questo tipo, e l'export lo esclude sempre
    /// (vedi <c>SearchExporter</c>) — resta visibile solo nella risposta della ricerca, per la griglia.
    /// </para>
    /// </summary>
    Custom
}

public static class FieldkindTranslator
{ 
    public static string Translate(FieldKind kind)
    {
        return kind switch
        {
            FieldKind.String => "String",
            FieldKind.Integer => "Int",
            FieldKind.Decimal => "Float",
            FieldKind.Boolean => "Bool",
            FieldKind.DateTime => "Datetime",
            FieldKind.Guid => "Guid",
            FieldKind.Enum => "String",
            FieldKind.ObjectId => "String",
            FieldKind.Link => "Link",
            FieldKind.Custom => "Custom",
            _ => throw new Exception($"{kind} not handled")
        };
    }
}
/*
public static readonly string String = "string";
public static readonly string Int = "int";
public static readonly string Bool = "bool";
public static readonly string Float = "float";
public static readonly string Date = "date";
public static readonly string Datetime = "datetime";
public static readonly string Guid = "guid";
public static readonly string Custom = "custom";
public static readonly string Image = "image";
public static readonly string Link = "link";
*/