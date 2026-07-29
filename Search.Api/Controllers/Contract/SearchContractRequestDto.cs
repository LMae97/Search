using Search.Api.Dto;

namespace Search.Api.Controllers.Contract;

/// <summary>
/// Richiesta di ricerca contratti: la parte comune (<see cref="BaseSearchRequestDto"/>) più i filtri
/// tipizzati specifici di questo contesto, tradotti da <see cref="ContractBaseFilters"/> — stesso ruolo
/// del vecchio <c>SearchContractRequestDto</c>/<c>SearchContractBaseFilters</c>.
/// </summary>
public sealed class SearchContractRequestDto : BaseSearchRequestDto
{
    public DateOnlyRangeDto? SignatureDate { get; set; }
    public DateOnlyRangeDto? CreatedAtDate { get; set; }
    public List<Guid>? Status { get; set; }
    public List<Guid>? OrgMembers { get; set; }
    public List<Guid>? Organizations { get; set; }
    public List<Guid>? WorkProfiles { get; set; }
    public List<Guid>? Brands { get; set; }
    public List<Guid>? Assignees { get; set; }
    public List<string>? StatusType { get; set; }
    public List<string>? AutomationStatus { get; set; }
    public List<bool>? Paid { get; set; }
    public List<string>? Products { get; set; }         // Nomi prodotto
    public List<string>? ProductOptions { get; set; }   // Codici (id) opzione prodotto

    public List<string>? ProductSuperstates { get; set; }
    public List<Guid>? ProductCategories { get; set; }
}
