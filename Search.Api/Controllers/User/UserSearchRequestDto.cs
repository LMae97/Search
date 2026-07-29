using WeByte.Search.Api.Dto;

namespace WeByte.Search.Api.Controllers.User;

public sealed class UserSearchRequestDto : BaseSearchRequestDto
{
    public List<Guid>? Organizations { get; set; }
    public List<Guid>? Brands { get; set; }
    public List<Guid>? WorkProfiles { get; set; }
    public List<Guid>? Roles { get; set; }
    public List<Guid>? Types { get; set; }
    public List<Guid>? Tags { get; set; }
    public List<bool>? IsActive { get; set; }
}
