namespace Search.Domain.Common;

/// <summary>
/// Metadato di cancellazione logica (soft delete). Il record resta nel DB ma viene
/// escluso dalle query tramite un filtro globale (EF: <c>HasQueryFilter</c>;
/// Mongo: un filtro applicato dal repository).
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAt { get; }

    string? DeletedBy { get; }
}
