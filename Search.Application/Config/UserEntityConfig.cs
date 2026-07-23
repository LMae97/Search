using Search.Application.Querying;
using Search.Application.Querying.Dynamic;

namespace Search.Application.Config;

public class UserEntityConfig : ISearchableEntityConfig
{
    public SearchEntity SearchEntity => SearchEntity.RelationalRaw(SearchableEntityNameDict.User);
    public IReadOnlyList<string> DefaultProjection => [
        "id",
        "username",
        "email"
    ];
    public IReadOnlyList<SortField> DefaultSort => [
        new SortField("username", SortDirection.Ascending),
        new SortField("id", SortDirection.Ascending)
    ];
}