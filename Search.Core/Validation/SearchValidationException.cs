namespace Search.Application.Querying.Validation;

/// <summary>
/// Richiesta di ricerca non valida. Raccoglie <b>tutti</b> gli errori in un colpo solo
/// (campo sconosciuto, operatore non ammesso, arità dei valori errata): l'API la traduce in 422.
/// </summary>
public sealed class SearchValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public SearchValidationException(IReadOnlyList<string> errors)
        : base("Richiesta di ricerca non valida: " + string.Join(" | ", errors))
    {
        Errors = errors;
    }
}
