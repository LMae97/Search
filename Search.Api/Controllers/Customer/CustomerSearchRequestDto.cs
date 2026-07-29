using WeByte.Search.Api.Dto;

namespace WeByte.Search.Api.Controllers.Customer;

public sealed class CustomerSearchRequestDto : BaseSearchRequestDto
{
    public List<Guid>? Tags { get; set; }
}