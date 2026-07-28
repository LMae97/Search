using Search.Core;
using Search.Core.Dynamic;

namespace Search.Application.Config;

public class ContractEntityConfig : ISearchableEntityConfig
{
    public SearchEntity SearchEntity => SearchEntity.Document(SearchableEntityNameDict.Contract);

    public IReadOnlyList<string> DefaultProjection => [
        "id"
    ];

    public IReadOnlyList<SortField> DefaultSort => [
        new SortField("createdAt", SortDirection.Descending),
        new SortField("id", SortDirection.Ascending)
    ];

    public string IdField => "id";

    public IReadOnlyList<string> HiddenProjection => [
        IdField
    ];
}
