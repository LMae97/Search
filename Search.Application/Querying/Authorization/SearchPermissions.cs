namespace Search.Application.Querying.Authorization;

/// <summary>
/// Placeholder dei permessi usati nella demo. In un progetto reale questi Guid verrebbero
/// dal catalogo permessi dell'applicazione (DB/identity), non hardcodati qui.
/// </summary>
public static class SearchPermissions
{
    public static readonly Guid ViewPrice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ViewAudit = Guid.Parse("22222222-2222-2222-2222-222222222222");
}
