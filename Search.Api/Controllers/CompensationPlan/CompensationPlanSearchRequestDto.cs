using WeByte.Search.Api.Dto;

namespace WeByte.Search.Api.Controllers.CompensationPlan;

public sealed class CompensationPlanSearchRequestDto : BaseSearchRequestDto
{
    public List<Guid>? Brands { get; set; }

    public List<Guid>? WorkProfiles { get; set; }

    public DateOnlyRangeDto? Validity { get; set; }
}
