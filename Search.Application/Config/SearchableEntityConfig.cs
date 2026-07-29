using WeByte.Search.Core;
using WeByte.Search.Core.Dynamic;
using WeByte.Search.Core.Filters;

namespace WeByte.Search.Application.Config;

public interface ISearchableEntityConfig
{
    SearchEntity SearchEntity { get; }
    IReadOnlyList<string> HiddenProjection { get; }
    IReadOnlyList<string> DefaultProjection { get; }
    IReadOnlyList<SortField> DefaultSort { get; }
    string IdField { get; }
    public FilterNode? AuthFilters(IVisibilityFilters? filters);

    string? MongoAtlasIndex => null;
    string? MongoUnwindPath => null;
}