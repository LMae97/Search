using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Dynamic;

/// <summary>
/// Definizione di un campo di ricerca <b>dinamico</b>, specifico per un tenant (<see cref="SpaceId"/>).
/// A differenza dei campi statici non ha un selettore CLR: punta direttamente a un path nello store
/// (<see cref="StoragePath"/>, es. "attributes.zonaConsegna" su un documento Mongo).
/// In produzione queste righe vivono a DB; qui è un semplice record.
/// </summary>
public sealed record DynamicFieldDefinition(
    Guid SpaceId,
    string EntityName,
    string Name,
    FieldKind Kind,
    bool IsArray,
    string StoragePath,
    string? Label = null,
    string? Section = null,
    Guid? RequiredPermissionId = null);
