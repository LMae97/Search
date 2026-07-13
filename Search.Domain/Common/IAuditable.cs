namespace Search.Domain.Common;

/// <summary>
/// Colonne di audit comuni. Popolate automaticamente dall'infrastruttura
/// (es. un interceptor di EF Core o un wrapper del repository Mongo),
/// leggendo l'utente corrente da un <c>IUserContext</c> e l'istante da un <c>IClock</c>.
/// </summary>
public interface IAuditable
{
    /// <summary>DATA CREAZIONE (UTC).</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>UTENTE CREAZIONE (id o email).</summary>
    string CreatedBy { get; }

    /// <summary>DATA ULTIMA MODIFICA (UTC). Null finché il record non viene modificato.</summary>
    DateTimeOffset? LastModifiedAt { get; }

    /// <summary>UTENTE ULTIMA MODIFICA. Null finché il record non viene modificato.</summary>
    string? LastModifiedBy { get; }
}
