using Search.Api.Dto;

namespace Search.Api.Controllers.Customer;

public sealed class CustomerSearchRequestDto : BaseSearchRequestDto
{
    public List<Guid>? Tags { get; set; }
}