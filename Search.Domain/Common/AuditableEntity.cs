namespace Search.Domain.Common;

/// <summary>
/// Entità con colonne di audit e soft delete. I setter sono privati: lo stato di audit
/// si modifica solo attraverso i metodi dedicati, così il modello resta sempre coerente.
/// </summary>
public abstract class AuditableEntity<TKey> : Entity<TKey>, IAuditable, ISoftDeletable
    where TKey : notnull
{
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset? LastModifiedAt { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    protected AuditableEntity(TKey id) : base(id)
    {
    }

    protected AuditableEntity()
    {
    }

    // --- Hook di audit invocati dall'infrastruttura (interceptor / repository). ---
    // Non vanno chiamati dal codice di dominio: l'audit è una preoccupazione trasversale.

    /// <summary>Marca la creazione. Idempotente: lo stamp di creazione si scrive una sola volta.</summary>
    public void ApplyCreationAudit(string user, DateTimeOffset timestamp)
    {
        if (CreatedAt != default)
            return;
        CreatedBy = RequireUser(user);
        CreatedAt = timestamp;
    }

    /// <summary>Marca l'ultima modifica.</summary>
    public void ApplyModificationAudit(string user, DateTimeOffset timestamp)
    {
        LastModifiedBy = RequireUser(user);
        LastModifiedAt = timestamp;
    }

    // --- Soft delete: è una decisione di dominio, invocata esplicitamente dai casi d'uso. ---

    public void MarkAsDeleted(string user, DateTimeOffset timestamp)
    {
        if (IsDeleted)
            return;
        IsDeleted = true;
        DeletedBy = RequireUser(user);
        DeletedAt = timestamp;
        ApplyModificationAudit(user, timestamp);
    }

    public void Restore(string user, DateTimeOffset timestamp)
    {
        if (!IsDeleted)
            return;
        IsDeleted = false;
        DeletedBy = null;
        DeletedAt = null;
        ApplyModificationAudit(user, timestamp);
    }

    private static string RequireUser(string user) =>
        string.IsNullOrWhiteSpace(user)
            ? throw new ArgumentException("L'utente di audit è obbligatorio.", nameof(user))
            : user;
}
