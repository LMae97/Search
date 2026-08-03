using WeByte.Search.Api.Dto;
using WeByte.Search.Core.Filters;

namespace WeByte.Search.Api.Controllers.Contract;

/// <summary>
/// Traduce i filtri tipizzati di <see cref="SearchContractRequestDto"/> in <see cref="FilterNode"/> — stesso
/// ruolo del vecchio <c>SearchContractBaseFilters.GetBaseFilters</c>, ma verso il motore nuovo: qui il
/// risultato è direttamente un nodo dell'albero <c>SearchWithCount.Core.Filters</c>, non un <c>Filter</c>/<c>OperationType</c>
/// da tradurre altrove.
/// </summary>
public static class ContractBaseFilters
{
    // Sentinelle per "Assignees": significano "nessun assegnatario" / "un assegnatario qualsiasi" / "assegnato a me".
    public static readonly Guid NoneOption = Guid.Parse("00000000-0000-0000-0000-000000000000");
    public static readonly Guid NotNoneOption = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid OwnOption = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public static FilterNode? GetBaseFilters(SearchContractRequestDto req, Guid currentUserId)
    {
        var filters = new List<FilterNode>();

        BaseSearchRequestDtoAdapter.AddIn(filters, "statusId", req.Status?.Select(id => id.ToString()));
        BaseSearchRequestDtoAdapter.AddIn(filters, "statusTypeId", req.StatusType);
        BaseSearchRequestDtoAdapter.AddIn(filters, "automationStatusResumeId", req.AutomationStatus);
        BaseSearchRequestDtoAdapter.AddIn(filters, "orgMemberId", req.OrgMembers?.Select(id => (object?)id));
        BaseSearchRequestDtoAdapter.AddIn(filters, "brandId", req.Brands?.Select(id => (object?)id));
        BaseSearchRequestDtoAdapter.AddIn(filters, "workProfileId", req.WorkProfiles?.Select(id => (object?)id));
        BaseSearchRequestDtoAdapter.AddIn(filters, "product", req.Products);
        BaseSearchRequestDtoAdapter.AddIn(filters, "optionIds", req.ProductOptions);
        BaseSearchRequestDtoAdapter.AddIn(filters, "productStatusTypeId", req.ProductSuperstates);
        BaseSearchRequestDtoAdapter.AddIn(filters, "productCategoryId", req.ProductCategories?.Select(id => id.ToString()));
        BaseSearchRequestDtoAdapter.AddIn(filters, "organizationId", req.Organizations?.Select(id => (object?)id));

        AddAssignees(filters, req.Assignees, currentUserId);
        BaseSearchRequestDtoAdapter.AddDateRange(filters, "signatureDate", req.SignatureDate);
        BaseSearchRequestDtoAdapter.AddDateRange(filters, "createdAt", req.CreatedAtDate);
        AddPaidTriState(filters, req.Paid);

        return BaseSearchRequestDtoAdapter.Combine(LogicalOperator.And, filters);
    }

    // "Assignees" è multi-valore CON sentinelle speciali: gli id normali vanno in "in", le sentinelle
    // diventano condizioni separate, tutte in OR tra loro (stessa logica del vecchio traduttore).
    private static void AddAssignees(List<FilterNode> filters, List<Guid>? assignees, Guid currentUserId)
    {
        if (assignees is not { Count: > 0 }) return;

        var ids = assignees.Where(id => id != NoneOption && id != NotNoneOption && id != OwnOption).ToList();
        var branches = new List<FilterNode>();

        if (ids.Count > 0)
            branches.Add(Filter.In("assignedToId", ids.Cast<object?>().ToArray()));
        if (assignees.Contains(NoneOption))
            branches.Add(Filter.IsNull("assignedToId"));
        if (assignees.Contains(NotNoneOption))
            branches.Add(Filter.IsNotNull("assignedToId"));
        if (assignees.Contains(OwnOption))
            branches.Add(Filter.Eq("assignedToId", currentUserId));

        var combined = BaseSearchRequestDtoAdapter.Combine(LogicalOperator.Or, branches);
        if (combined is not null)
            filters.Add(combined);
    }

    // "Paid" tri-stato: solo pagati / solo non pagati / entrambi selezionati = nessun filtro (come il vecchio
    // traduttore). "paymentFinalizedAt" valorizzata = pagato; null = non pagato.
    private static void AddPaidTriState(List<FilterNode> filters, List<bool>? paid)
    {
        if (paid is not { Count: > 0 }) return;

        var includePaid = paid.Contains(true);
        var includeUnpaid = paid.Contains(false);
        if (includePaid && includeUnpaid) return;

        filters.Add(includePaid ? Filter.IsNotNull("paymentFinalizedAt") : Filter.IsNull("paymentFinalizedAt"));
    }
}
