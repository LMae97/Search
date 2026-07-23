using Search.Application.Querying;
using Search.Application.Querying.Dynamic;

namespace Search.Application.Config;

public class CompensationPlanEntityConfig : ISearchableEntityConfig
{
    // Store documentale (Mongo): il nome dell'entità è la chiave delle definizioni campi;
    // il mapping verso la collection reale ("CompensationPlan") vive nel MongoCollectionProvider.
    public SearchEntity SearchEntity => SearchEntity.Document(SearchableEntityNameDict.CompensationPlan);

    public IReadOnlyList<string> DefaultProjection => [
        "id",
        "name",
        "description",
        "createdAt"
    ];

    public IReadOnlyList<SortField> DefaultSort => [
        new SortField("name", SortDirection.Ascending),
        new SortField("id", SortDirection.Ascending)
    ];
}