using Search.Core;
using Search.Core.Metadata;

namespace Search.Application.Dtos;

public class SearchResponseDto
{
    public List<HeaderDto> Header { get; set; } = [];
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Body { get; set; } = [];
    public long ResultCount { get; set; }
}

public class HeaderDto
{
    public string Id { get; init; }
    public string Key { get; init; }
    public string Label { get; init; }
    public string Type { get; init; }
    public bool Visible { get; init; }

    public static HeaderDto FromDescriptor(FieldDescriptor field) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Key = field.Name,
        Label = field.Label,
        Type = AdaptType(field.Kind, field.IsArray),
        Visible = !field.IsHidden
    };

    private static string AdaptType(FieldKind kind, bool isArray) =>
        isArray ? kind.ToString() + "[]" : kind.ToString();
}

public class SearchResponseAdapter
{
    public static SearchResponseDto ToSearchResponseDto(
        IEnumerable<FieldDescriptor> projection,
        SearchResult<IReadOnlyDictionary<string, object?>> result)
    {
        return new SearchResponseDto
        {
            Header = projection.Select(x => HeaderDto.FromDescriptor(x)).ToList(),
            Body = result.Items.Select(x => x).Where(x => x != null).ToList(),
            ResultCount = result.TotalCount
        };
    }
}