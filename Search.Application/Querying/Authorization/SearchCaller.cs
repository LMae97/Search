namespace Search.Application.Querying.Authorization;

/// <summary>
/// Chi sta eseguendo la ricerca. Lo fornisce il layer web a partire dall'utente autenticato.
/// Serve a costruire la <see cref="EffectiveSearchMap"/> (campi visibili per questo utente/tenant).
/// </summary>
public sealed record SearchCaller(Guid SpaceId, IReadOnlySet<Guid> Permissions);
