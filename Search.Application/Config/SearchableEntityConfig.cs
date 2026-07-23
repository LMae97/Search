using Search.Application.Querying;
using Search.Application.Querying.Dynamic;

namespace Search.Application.Config;

public interface ISearchableEntityConfig
{
    SearchEntity SearchEntity { get; }
    IReadOnlyList<string> DefaultProjection { get; }
    IReadOnlyList<SortField> DefaultSort { get; }

}