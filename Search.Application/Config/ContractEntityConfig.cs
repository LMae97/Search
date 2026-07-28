using Search.Core;
using Search.Core.Dynamic;
using Search.Core.Filters;

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

    public FreeTextSearch FreeText => FreeTextSearch.Atlas("Contract");

    public FilterNode? AuthFilters(IVisibilityFilters? filters)
    {
        if (filters == null) return null;

        var ctrFilters = (ContractVisibilityFilters)filters;

        FilterNode? wpFilter = null;

        if (ctrFilters.WorkProfileIds != null)
        {
            wpFilter = ctrFilters.WorkProfileIds.Count > 0
                ? Filter.In("workProfileId", ctrFilters.WorkProfileIds.Cast<object?>().ToArray())
                : Filter.Eq("workProfileId", Guid.Empty);
        }

        var optFilters = new List<FilterNode>();

        if (ctrFilters.OrgMemberIds != null)
            optFilters.Add(Filter.In("orgMemberId", ctrFilters.OrgMemberIds.Cast<object?>().ToArray()));

        if (ctrFilters.AssignedToId != null)
            optFilters.Add(Filter.And(
                Filter.Eq("assignedToId", ctrFilters.AssignedToId),
                Filter.NotIn("statusTypeId", ["completed", "rejected"])));

        if (ctrFilters.AssignedToIds != null)
            optFilters.Add(Filter.And(
                Filter.In("assignedToId", ctrFilters.AssignedToIds.Cast<object?>().ToArray()),
                Filter.NotIn("statusTypeId", ["completed", "rejected"])));

        if (ctrFilters.OrganizationIds != null)
            optFilters.Add(Filter.In("organizationId", ctrFilters.OrganizationIds.Cast<object?>().ToArray()));

        if (optFilters.Count == 0) return wpFilter;

        var orOptFilters = Filter.Or([.. optFilters]);

        return wpFilter != null
            ? Filter.And(wpFilter, orOptFilters)
            : orOptFilters;
    }
}

public class ContractVisibilityFilters : IVisibilityFilters
{
    //Filtri applicati in OR tra loro, tranne per WorkProfileIds (in AND con gli standard)

    // --OWN --
    public Guid? AssignedToId { get; set; } = null; //WHERE assignedToId = assignedToId
    public List<Guid>? OrgMemberIds { get; set; } = null; //WHERE orgMemberId IN (orgMemberIds)

    // -- ORG --
    public List<Guid>? OrganizationIds { get; set; } = null; //WHERE organizationId IN (organizationIds)
    public List<Guid>? AssignedToIds { get; set; } = null; //WHERE assignedToId IN (assignedToIds)

    //Filtro applicato in AND con i precedenti
    public List<Guid>? WorkProfileIds { get; set; } = null; //WHERE workProfileId IN (workProfileIds)
}