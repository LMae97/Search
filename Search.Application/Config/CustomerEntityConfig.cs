using WeByte.Search.Core;
using WeByte.Search.Core.Dynamic;
using WeByte.Search.Core.Filters;

namespace WeByte.Search.Application.Config;

public class CustomerEntityConfig : ISearchableEntityConfig
{
    public SearchEntity SearchEntity => SearchEntity.RelationalRaw(SearchableEntityNameDict.Customer);

    public IReadOnlyList<string> DefaultProjection => [
        "id",
        "firstName",
        "lastName",
        "email"
    ];

    public IReadOnlyList<SortField> DefaultSort => [
        new SortField("name", SortDirection.Ascending) ,
        new SortField("id", SortDirection.Ascending)
    ];

    public string IdField => "id";

    public IReadOnlyList<string> HiddenProjection => [
        IdField
    ];

    public FilterNode? AuthFilters(IVisibilityFilters? filters)
    {
        if (filters == null) return null;

        var cusFilters = (CustomerVisibilityFilters)filters;

        var optFilters = new List<FilterNode>();

        if (cusFilters.OrgMemberIds != null)
            optFilters.Add(Filter.ArrayContainsAny("contractOrgMemberIds", cusFilters.OrgMemberIds.Cast<object?>().ToArray()));

        if (cusFilters.AssignedToId != null)
            optFilters.Add(Filter.ArrayContains("contractAssignedToIds", cusFilters.AssignedToId));

        if (cusFilters.OrgMemberOrganizationId != null)
            optFilters.Add(Filter.ArrayContains("contractOrgMemberOrganizationIds", cusFilters.OrgMemberOrganizationId));

        if (cusFilters.AssignedToOrganizationId != null)
            optFilters.Add(Filter.ArrayContains("contractAssignedToOrganizationIds", cusFilters.AssignedToOrganizationId));

        if (optFilters.Count == 0) return null;

        return Filter.Or([.. optFilters]);
    }
}

public class CustomerVisibilityFilters : IVisibilityFilters
{
    //Filtri applicati in OR tra loro (in AND con gli standard)
    public List<Guid>? OrgMemberIds { get; set; } = null; //WHERE id IN (orgMemberIds)
    public Guid? AssignedToId { get; set; } = null; //WHERE assignedToId = assignedToId
    public Guid? OrgMemberOrganizationId { get; set; } = null; //WHERE id IN (organizationsIds)
    public Guid? AssignedToOrganizationId { get; set; } = null; //WHERE assignedToOrganizationId = assignedToOrganizationId
}