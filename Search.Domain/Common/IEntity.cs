namespace Search.Domain.Common;

/// <summary>
/// Contratto minimo di un'entità di dominio: possiede un'identità stabile di tipo
/// <typeparamref name="TKey"/>. La covarianza (<c>out</c>) permette di trattare
/// <c>IEntity&lt;Guid&gt;</c> come <c>IEntity&lt;object&gt;</c> dove serve.
/// </summary>
public interface IEntity<out TKey>
{
    TKey Id { get; }
}
