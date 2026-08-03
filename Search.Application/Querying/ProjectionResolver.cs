using WeByte.Search.Application.Config;
using WeByte.Search.Application.Search;
using WeByte.Search.Core;
using WeByte.Search.Core.Metadata;

namespace WeByte.Search.Application.Querying;

/// <summary>
/// Risolve la proiezione <b>effettiva</b> di una richiesta: proiezione esplicita → <c>DefaultProjection</c>
/// della mappa (campi con <c>DefaultOrder</c>) → <c>DefaultProjection</c> della config, sempre unita ai
/// campi di <c>HiddenProjection</c> (restano filtrabili/ordinabili anche se non proiettati).
/// <para>
/// Pura: nessuna esecuzione, nessun accesso allo store — solo <see cref="IEntitySearchMap"/> e
/// <see cref="ISearchableEntityConfig"/>, già in memoria. È la stessa regola che <see cref="SearchHandlerBase"/>
/// applica prima di eseguire; condividerla è ciò che permette all'export di conoscere il numero <b>esatto</b>
/// di colonne prima di lanciare qualunque query, invece di stimarlo.
/// </para>
/// </summary>
public static class ProjectionResolver
{
    public static IReadOnlyList<string> Resolve(IEntitySearchMap map, ISearchableEntityConfig config, SearchRequest request)
    {
        var basePrj = config.HiddenProjection.ToHashSet();

        var reqPrj = request.Projection;
        var mapPrj = map.DefaultProjection();
        var configPrj = config.DefaultProjection;

        var prj = reqPrj.Count > 0 ? reqPrj
            : mapPrj.Count > 0 ? mapPrj
            : configPrj;

        basePrj.UnionWith(prj.ToHashSet());

        return [.. basePrj];
    }
}
