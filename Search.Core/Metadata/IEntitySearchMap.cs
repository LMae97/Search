using System.Diagnostics.CodeAnalysis;

namespace Search.Application.Querying.Metadata;

/// <summary>
/// Mappa di ricerca di un'entità: l'insieme dei campi esposti e le loro regole.
/// È la fonte di verità per validazione e traduzione, e la whitelist di sicurezza.
/// </summary>
public interface IEntitySearchMap
{
    string EntityName { get; }

    IReadOnlyDictionary<string, FieldDescriptor> Fields { get; }

    bool TryGetField(string name, [MaybeNullWhen(false)] out FieldDescriptor descriptor);
    /*
    [MaybeNullWhen(false)] out FieldDescriptor descriptor
    significa:
    - Se il metodo restituisce true, descriptor contiene un valore valido.
    - Se il metodo restituisce false, descriptor potrebbe essere null
    */
}
