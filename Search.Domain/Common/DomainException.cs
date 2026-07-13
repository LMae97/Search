namespace Search.Domain.Common;

/// <summary>
/// Violazione di una regola/invariante di dominio. Va tradotta dall'applicazione
/// in un errore 4xx (es. 422 Unprocessable Entity), non in un 500.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>Piccolo guard per esprimere le invarianti in modo leggibile.</summary>
internal static class DomainGuard
{
    /// <summary>Lancia <see cref="DomainException"/> se <paramref name="condition"/> è vera.</summary>
    public static void Against(bool condition, string message)
    {
        if (condition)
            throw new DomainException(message);
    }
}
