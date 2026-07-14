using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Authorization;

/// <summary>
/// Costruisce la mappa <b>effettiva</b> per un <see cref="SearchCaller"/>: tiene solo i campi che
/// l'utente può vedere in base ai permessi, restituendo una <see cref="MaterializedSearchMap"/> filtrata.
/// <para>
/// È il cuore dell'autorizzazione: "niente permesso" -> "campo assente". Di conseguenza validazione,
/// sanitizer, proiezione ed endpoint dei campi lo ignorano tutti a cascata, senza controlli sparsi.
/// </para>
/// </summary>
public static class EffectiveSearchMap
{
    public static IEntitySearchMap For(string entityName, IEnumerable<FieldDescriptor> fields, SearchCaller caller)
        => new MaterializedSearchMap(entityName, fields.Where(field => IsAuthorized(field, caller)));

    private static bool IsAuthorized(FieldDescriptor field, SearchCaller caller)
        => field.RequiredPermissionId is null
           || caller.Permissions.Contains(field.RequiredPermissionId.Value);
}
