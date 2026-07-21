using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Dynamic;

/// <summary>
/// Definizione a DB di un campo di ricerca — unica per campi statici e dinamici, per entrambi gli store.
/// <list type="bullet">
/// <item><see cref="Path"/>: relazionale = property-path CLR ("Price.Amount"); documentale = path Mongo ("customer.email").</item>
/// <item><see cref="SpaceId"/>: null = campo globale (vale per tutti i tenant); valorizzato = dinamico del tenant.</item>
/// </list>
/// In produzione è una riga di tabella; qui un semplice record.
/// </summary>
public sealed record SearchFieldDefinition(
    string EntityName,
    string Name,
    FieldKind Kind,
    bool JsonColumn,                    // se il campo è memorizzato in una colonna JSONB (Postgres)
    bool IsArray,
    string Path,
    string? Label = null,
    string? Section = null,
    int? DefaultOrder = null,           // proiezione -> ordine di proiezione fallback (null = non proiettato di default)
    bool IsHidden = false,              // proiezione -> se il campo è nascosto (non proiettabile. Rimane filtrabile/ordinabile)
    Guid? RequiredPermissionId = null,
    Guid? SpaceId = null);