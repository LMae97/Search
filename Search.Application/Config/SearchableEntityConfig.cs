using Search.Core;
using Search.Core.Dynamic;
using Search.Core.Filters;

namespace Search.Application.Config;

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