namespace Search.Application.Querying.Dynamic;

/// <summary>Tipo di store di un'entità, che determina come si interpreta il path di un campo.</summary>
public enum StoreKind
{
    /// <summary>Relazionale (EF/Postgres): il path è una property-path CLR, ricostruita in Expression.</summary>
    Postgres,

    /// <summary>Documentale (Mongo): il path è il path del documento, usato così com'è.</summary>
    Mongo
}
