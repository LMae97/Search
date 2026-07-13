namespace Search.Application.Querying.Filters;

/// <summary>
/// Operatori applicabili a un campo. Quali siano ammessi per uno specifico campo dipende dal
/// suo tipo e dall'essere array: la decisione sta nel metadata registry, non qui.
/// </summary>
public enum FilterOperator
{
    // Uguaglianza (tutti i tipi)
    Equals,
    NotEquals,

    // Ordinamento (numeri, date)
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Between,

    // Insiemi
    In,
    NotIn,

    // Stringhe
    Contains,
    NotContains,
    StartsWith,
    EndsWith,

    // Nullità
    IsNull,
    IsNotNull,

    // Campi array/collezione
    ArrayContains,       // la collezione contiene l'elemento indicato
    ArrayContainsAny,    // interseca almeno uno dei valori indicati
    ArrayContainsAll,    // contiene tutti i valori indicati
    ArrayIsEmpty,
    ArrayNotEmpty
}
