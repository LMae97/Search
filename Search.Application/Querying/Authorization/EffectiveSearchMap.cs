using System.Diagnostics.CodeAnalysis;
using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Authorization;

/// <summary>
/// Mappa "effettiva" per uno specifico <see cref="SearchCaller"/>: parte dalla mappa completa
/// (codice) e tiene solo i campi che l'utente può vedere in base ai permessi.
/// <para>
/// È il cuore dell'autorizzazione: un campo non autorizzato semplicemente NON è nella mappa.
/// Di conseguenza validazione, sanitizzazione, proiezione ed endpoint dei campi lo ignorano
/// tutti, senza controlli sparsi. "Niente permesso" ≡ "campo assente".
/// </para>
/// </summary>
public sealed class EffectiveSearchMap : IEntitySearchMap
{
    private readonly Dictionary<string, FieldDescriptor> _fields;

    public string EntityName { get; }

    public EffectiveSearchMap(IEntitySearchMap source, SearchCaller caller)
    {
        EntityName = source.EntityName;
        _fields = source.Fields.Values
            .Where(field => IsAuthorized(field, caller))
            .ToDictionary(field => field.Name, field => field, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, FieldDescriptor> Fields => _fields;

    public bool TryGetField(string name, [MaybeNullWhen(false)] out FieldDescriptor descriptor)
        => _fields.TryGetValue(name, out descriptor);

    private static bool IsAuthorized(FieldDescriptor field, SearchCaller caller)
        => field.RequiredPermissionId is null
           || caller.Permissions.Contains(field.RequiredPermissionId.Value);
}
