using WeByte.Search.Application.Config;
using WeByte.Search.Core;

namespace WeByte.Search.Application.Querying;

/// <summary>
/// Risolve l'ordinamento <b>effettivo</b> di una richiesta.
/// </summary>
public static class SortResolver
{
    public static IReadOnlyList<SortField> Resolve(ISearchableEntityConfig config, SearchRequest req)
    {
        var defaultSorting = config.IdField;

        var reqSort = req.Sort.ToList();
        var configSort = config.DefaultSort.ToList();

        var sort = reqSort.Count > 0 ? reqSort : configSort;
        if (sort.Any(x => x.Field == defaultSorting)) return sort;

        sort.Add(new SortField(defaultSorting, SortDirection.Ascending));

        return sort;
    }
}