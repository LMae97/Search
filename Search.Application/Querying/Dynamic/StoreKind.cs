namespace Search.Application.Querying.Dynamic;

/// <summary>Tipo di store di un'entità, che determina come si interpreta il path di un campo.</summary>
public enum StoreKind
{
    /// <summary>Relazionale (EF/PostgresEF): il path è una property-path CLR, ricostruita in Expression.</summary>
    PostgresEF,
    /// <summary>Relazionale grezzo (PostgresRaw): il path è direttamente un'espressione-colonna SQL fidata
    /// (es. <c>"brand"."Code"</c>, o un'estrazione JSONB), presa dalla whitelist e usata così com'è nel testo SQL.</summary>
    PostgresRaw,
    /// <summary>Documentale (Mongo): il path è il path del documento, usato così com'è.</summary>
    Mongo
}
